using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Backend.Services.Llm;
using Backend.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

// Config
var cfg         = builder.Configuration;
var catalogUrl  = cfg["CatalogUrl"]  ?? "https://test.controldepropiedades.com/api/propiedades/miraiz";
var cacheSecs   = int.TryParse(cfg["CacheSeconds"], out var s) ? s : 90;
var apiKey      = cfg["API_KEY"] ?? Environment.GetEnvironmentVariable("BACK_API_KEY") ?? "demo-key";
var llmProvider = (cfg["LLM_PROVIDER"] ?? "mock").ToLowerInvariant();
var llmApiKey   = cfg["LLM_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
var llmModel    = cfg["LLM_MODEL"] ?? cfg["OpenAI:Model"] ?? "gpt-4o-mini";
var propsLiteUrl = cfg["PropsLiteBaseUrl"] ?? "http://localhost:5002/api/propiedades/miraiz";
var debugEnabled = bool.TryParse(cfg["Debug:Enable"] ?? Environment.GetEnvironmentVariable("DEBUG_ENABLE"), out var de) && de;

// JSON options (con tildes en claves)
var jsonOpts = new JsonSerializerOptions {
  PropertyNamingPolicy = null,
  WriteIndented = false,
  Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};

// Rate Limiter para /api/nlq
builder.Services.AddRateLimiter(options =>
{
  options.AddFixedWindowLimiter("nlq", opt =>
  {
    opt.Window = TimeSpan.FromMinutes(1);
    opt.PermitLimit = 30;
    opt.QueueLimit = 0;
  });
});

// DI: PropsTool
builder.Services.AddSingleton<PropsTool>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var cache = sp.GetRequiredService<IMemoryCache>();
    var url = config["PropsLiteBaseUrl"] ?? "http://localhost:5002/api/propiedades/miraiz";
    return new PropsTool(url, cache);
});

// DI: ILlmClient (mock por defecto para /interactions)
builder.Services.AddSingleton<ILlmClient>(sp =>
{
  var factory = sp.GetRequiredService<IHttpClientFactory>();
  return llmProvider == "openai" && !string.IsNullOrWhiteSpace(llmApiKey)
    ? new LlmOpenAi(factory.CreateClient(), llmApiKey, llmModel)
    : new LlmMock(factory.CreateClient(), catalogUrl);
});

// DI: ILlmChat (para /api/nlq con function calling)
builder.Services.AddSingleton<ILlmChat>(sp =>
{
  var factory = sp.GetRequiredService<IHttpClientFactory>();
  var propsTool = sp.GetRequiredService<PropsTool>();
  return llmProvider == "openai" && !string.IsNullOrWhiteSpace(llmApiKey)
    ? new LlmChat(factory.CreateClient(), llmApiKey, llmModel, propsTool)
    : new LlmChatMock(propsTool);
});

var app = builder.Build();

// Rate limiter
app.UseRateLimiter();

// Exception handler (desarrollo)
app.Use(async (ctx, next) => {
  try {
    await next();
  } catch (Exception ex) {
    Console.WriteLine($"ERROR: {ex.Message}");
    Console.WriteLine($"STACK: {ex.StackTrace}");
    ctx.Response.StatusCode = 500;
    await ctx.Response.WriteAsJsonAsync(new { error = ex.Message, stack = ex.StackTrace });
  }
});

// CORS simple (demo) + preflight
app.Use(async (ctx, next) => {
  ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
  ctx.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
  ctx.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";
  if (ctx.Request.Method == "OPTIONS") { ctx.Response.StatusCode = 204; return; }
  await next();
});

// API-KEY para /interactions y /metrics
app.Use(async (ctx, next) => {
  var p = ctx.Request.Path.ToString();
  var protectedRoute = p.StartsWith("/interactions") || p.StartsWith("/metrics");
  if (!protectedRoute) { await next(); return; }
  var auth = ctx.Request.Headers.Authorization.ToString();
  if (auth == $"Bearer {apiKey}") { await next(); }
  else ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
});

var store = new InteractionStore();

// GET /properties  (proxy + cache 90s)
app.MapGet("/properties", async (HttpContext ctx, IMemoryCache cache, IHttpClientFactory httpFactory) =>
{
  if (cache.TryGetValue("props", out object? cached) && cached is not null)
  {
    var cachedJson = JsonSerializer.Serialize(cached, jsonOpts);
    var etag = $"\"{cachedJson.GetHashCode():X}\"";
    
    // Check If-None-Match
    if (ctx.Request.Headers.IfNoneMatch == etag)
    {
      ctx.Response.StatusCode = StatusCodes.Status304NotModified;
      return Results.StatusCode(304);
    }
    
    ctx.Response.Headers.ETag = etag;
    ctx.Response.Headers.CacheControl = $"public, max-age={cacheSecs}";
    return Results.Text(cachedJson, "application/json", Encoding.UTF8);
  }

  var http = httpFactory.CreateClient();
  http.Timeout = TimeSpan.FromSeconds(5);

  ApiResp? api = null;
  try {
    var response = await http.GetAsync(catalogUrl);
    
    // Si no está autorizado, retornar catálogo vacío o de ejemplo
    if (!response.IsSuccessStatusCode) {
      var fallbackData = new ApiResp { 
        success = true, 
        data = new List<PropertyItem>() 
      };
      cache.Set("props", fallbackData, TimeSpan.FromSeconds(cacheSecs));
      var fallbackJson = JsonSerializer.Serialize(fallbackData, jsonOpts);
      var fallbackEtag = $"\"{fallbackJson.GetHashCode():X}\"";
      ctx.Response.Headers.ETag = fallbackEtag;
      ctx.Response.Headers.CacheControl = $"public, max-age={cacheSecs}";
      return Results.Text(fallbackJson, "application/json", Encoding.UTF8);
    }
    
    api = await response.Content.ReadFromJsonAsync<ApiResp>();
  } catch (Exception ex) {
    // fallback en caso de error
    Console.WriteLine($"Error obteniendo catálogo: {ex.Message}");
    var fallbackData = new ApiResp { 
      success = true, 
      data = new List<PropertyItem>() 
    };
    cache.Set("props", fallbackData, TimeSpan.FromSeconds(cacheSecs));
    var fallbackJson = JsonSerializer.Serialize(fallbackData, jsonOpts);
    var fallbackEtag = $"\"{fallbackJson.GetHashCode():X}\"";
    ctx.Response.Headers.ETag = fallbackEtag;
    ctx.Response.Headers.CacheControl = $"public, max-age={cacheSecs}";
    return Results.Text(fallbackJson, "application/json", Encoding.UTF8);
  }

  // Normalizar claves con tilde (si vinieran sin tilde)
  if (api is not null && api.data is not null) {
    foreach (var p in api.data) {
      if (p.BanosSinTilde is not null && p.BanosConTilde is null) p.BanosConTilde = p.BanosSinTilde;
      if (p.AnoSinTilde   is not null && p.AnoConTilde   is null) p.AnoConTilde   = p.AnoSinTilde;
    }
  }

  cache.Set("props", api!, TimeSpan.FromSeconds(cacheSecs));
  var resultJson = JsonSerializer.Serialize(api, jsonOpts);
  var resultEtag = $"\"{resultJson.GetHashCode():X}\"";
  ctx.Response.Headers.ETag = resultEtag;
  ctx.Response.Headers.CacheControl = $"public, max-age={cacheSecs}";
  return Results.Text(resultJson, "application/json", Encoding.UTF8);
});

// GET /interactions?userId=...
app.MapGet("/interactions", (string? userId) => Results.Json(store.List(userId), jsonOpts));

// POST /interactions
app.MapPost("/interactions", async (HttpContext ctx, Interaction input, ILlmClient llm, IHttpClientFactory httpFactory) =>
{
  Console.WriteLine($"POST /interactions - UserId: {input.UserId}, PropiedadId: {input.PropiedadId}, Pregunta: {input.Pregunta}");
  
  if (ctx.Request.ContentLength is > 4096)
    return Results.BadRequest(new { error = "Body demasiado grande" });

  input.UserId   = Sanitize(input.UserId);
  input.Pregunta = Sanitize(input.Pregunta);
  
  if (string.IsNullOrWhiteSpace(input.UserId) || string.IsNullOrWhiteSpace(input.Pregunta)) {
    Console.WriteLine($"Validación fallida - UserId: '{input.UserId}', Pregunta: '{input.Pregunta}'");
    return Results.BadRequest(new { error = "Campos requeridos: userId, pregunta" });
  }
  if (input.Pregunta.Length > 500)
    return Results.BadRequest(new { error = "Pregunta muy larga" });
  if (IsPromptInjection(input.Pregunta))
    return Results.BadRequest(new { error = "Contenido no permitido" });

  input.Id        = Guid.NewGuid().ToString();
  input.Status    = "pendiente";
  input.CreatedAt = DateTime.UtcNow;

  // Construir contexto con la propiedad (solo campos necesarios)
  PropertyContext? propCtx = null;
  if (input.PropiedadId is not null) {
    var http = httpFactory.CreateClient();
    http.Timeout = TimeSpan.FromSeconds(5);
    
    try {
      var response = await http.GetAsync(catalogUrl, ctx.RequestAborted);
      if (response.IsSuccessStatusCode) {
        var data = await response.Content.ReadFromJsonAsync<ApiResp>(cancellationToken: ctx.RequestAborted);
        var x = data?.data?.FirstOrDefault(r => r.id == input.PropiedadId);
        if (x is not null) {
          propCtx = new PropertyContext(
            x.id, x.precio, x.habitaciones, x.BanosConTilde ?? x.BanosSinTilde,
            x.parqueos, x.m2construccion ?? x.area, x.ubicacion
          );
        }
      }
    } catch (Exception ex) {
      Console.WriteLine($"Error obteniendo propiedad {input.PropiedadId}: {ex.Message}");
      // Continuar sin contexto si falla
    }
  }

  var resp = await llm.AskAsync(new LlmRequest(input.Pregunta, propCtx), ctx.RequestAborted);
  input.Respuesta = resp.Answer;
  input.Status    = "respondida";
  store.Add(input);
  return Results.Json(input, jsonOpts);
});

// GET /metrics/interactions
app.MapGet("/metrics/interactions", () => Results.Json(store.Metrics(), jsonOpts));

// POST /api/nlq (NLQ con LLM + Function Calling)
app.MapPost("/api/nlq", async (NlqRequest req, ILlmChat llm, ILoggerFactory lf, CancellationToken ct) =>
{
  var sw = System.Diagnostics.Stopwatch.StartNew();
  var trace = Guid.NewGuid().ToString("N");
  var q = (req.query ?? "").Trim();
  if (string.IsNullOrWhiteSpace(q)) 
    return Results.BadRequest(new { success = false, error = "query vacío", trace_id = trace });

  try
  {
    var r = await llm.RunAsync(q, Math.Clamp(req.limit, 1, 100), req.estado, req.locale, ct);
    sw.Stop();
    return Results.Ok(new NlqResponse(true, r.Answer, r.ToolPayload, r.ToolArgs, sw.ElapsedMilliseconds, trace));
  }
  catch (Exception ex)
  {
    sw.Stop();
    lf.CreateLogger("NLQ").LogError(ex, "NLQ error {Trace}", trace);
    return Results.Problem(
      statusCode: 502,
      title: "NLQ_FAILED",
      detail: "No fue posible consultar el catálogo",
      extensions: new Dictionary<string, object?> { ["trace_id"] = trace, ["latency_ms"] = sw.ElapsedMilliseconds }
    );
  }
}).RequireRateLimiting("nlq");

// GET /api/propiedades/miraiz-lite (endpoint lite con field mask y cursor)
app.MapGet("/api/propiedades/miraiz-lite", async (
  HttpContext ctx,
  IMemoryCache cache,
  IHttpClientFactory httpFactory,
  string? fields,
  string? estado,
  int? afterId,
  int? limit) =>
{
  var cacheKey = $"lite:{fields}:{estado}:{afterId}:{limit}";
  
  if (cache.TryGetValue(cacheKey, out object? cached) && cached is not null)
    return Results.Text(JsonSerializer.Serialize(cached, jsonOpts), "application/json", Encoding.UTF8);

  var http = httpFactory.CreateClient();
  http.Timeout = TimeSpan.FromSeconds(5);

  ApiResp? api = null;
  try
  {
    var response = await http.GetAsync(catalogUrl);
    if (!response.IsSuccessStatusCode)
    {
      var fallbackData = new { success = true, data = new object[0], cursor = (int?)null };
      cache.Set(cacheKey, fallbackData, TimeSpan.FromSeconds(cacheSecs));
      return Results.Text(JsonSerializer.Serialize(fallbackData, jsonOpts), "application/json", Encoding.UTF8);
    }

    api = await response.Content.ReadFromJsonAsync<ApiResp>();
  }
  catch (Exception ex)
  {
    Console.WriteLine($"Error obteniendo catálogo lite: {ex.Message}");
    var fallbackData = new { success = true, data = new object[0], cursor = (int?)null };
    cache.Set(cacheKey, fallbackData, TimeSpan.FromSeconds(cacheSecs));
    return Results.Text(JsonSerializer.Serialize(fallbackData, jsonOpts), "application/json", Encoding.UTF8);
  }

  if (api is null || api.data is null)
  {
    var fallbackData = new { success = true, data = new object[0], cursor = (int?)null };
    return Results.Text(JsonSerializer.Serialize(fallbackData, jsonOpts), "application/json", Encoding.UTF8);
  }

  // Normalizar claves con tilde
  foreach (var p in api.data)
  {
    if (p.BanosSinTilde is not null && p.BanosConTilde is null) p.BanosConTilde = p.BanosSinTilde;
    if (p.AnoSinTilde is not null && p.AnoConTilde is null) p.AnoConTilde = p.AnoSinTilde;
  }

  // Filtrar por estado
  var filtered = api.data.AsEnumerable();
  if (!string.IsNullOrWhiteSpace(estado))
    filtered = filtered.Where(p => p.estado?.Equals(estado, StringComparison.OrdinalIgnoreCase) == true);

  // Cursor (afterId)
  if (afterId.HasValue)
    filtered = filtered.Where(p => p.id > afterId.Value);

  // Limit
  var maxLimit = Math.Clamp(limit ?? 20, 1, 100);
  var items = filtered.Take(maxLimit).ToList();

  // Field mask
  var requestedFields = (fields ?? "id,propiedad,precio,imagenes.url").Split(',', StringSplitOptions.RemoveEmptyEntries)
    .Select(f => f.Trim().ToLowerInvariant()).ToHashSet();

  var masked = items.Select(item =>
  {
    var obj = new Dictionary<string, object?>();
    
    if (requestedFields.Contains("id")) obj["id"] = item.id;
    if (requestedFields.Contains("propiedad")) obj["propiedad"] = item.propiedad;
    if (requestedFields.Contains("precio")) obj["precio"] = item.precio;
    if (requestedFields.Contains("area")) obj["area"] = item.area;
    if (requestedFields.Contains("tipo")) obj["tipo"] = item.tipo;
    if (requestedFields.Contains("ubicacion")) obj["ubicacion"] = item.ubicacion;
    if (requestedFields.Contains("estado")) obj["estado"] = item.estado;
    if (requestedFields.Contains("habitaciones")) obj["habitaciones"] = item.habitaciones;
    if (requestedFields.Contains("baños") || requestedFields.Contains("banos")) 
      obj["baños"] = item.BanosConTilde ?? item.BanosSinTilde;
    if (requestedFields.Contains("parqueos")) obj["parqueos"] = item.parqueos;
    if (requestedFields.Contains("m2construccion")) obj["m2construccion"] = item.m2construccion;
    if (requestedFields.Contains("año") || requestedFields.Contains("ano"))
      obj["año"] = item.AnoConTilde ?? item.AnoSinTilde;
    if (requestedFields.Contains("titulo")) obj["titulo"] = item.titulo;
    if (requestedFields.Contains("descripcion")) obj["descripcion"] = item.descripcion;
    if (requestedFields.Contains("latitud")) obj["latitud"] = item.latitud;
    if (requestedFields.Contains("longitud")) obj["longitud"] = item.longitud;
    
    if (requestedFields.Contains("imagenes.url") || requestedFields.Contains("imagenes"))
    {
      obj["imagenes"] = item.imagenes?.Select(img => new
      {
        tipo = img.tipo,
        url = img.url,
        formato = img.formato
      }).ToList();
    }

    if (requestedFields.Contains("proyecto"))
      obj["proyecto"] = item.proyecto;

    return obj;
  }).ToList();

  var nextCursor = items.Count == maxLimit ? items.Last().id : (int?)null;
  var result = new { success = true, data = masked, cursor = nextCursor };

  cache.Set(cacheKey, result, TimeSpan.FromSeconds(cacheSecs));
  return Results.Text(JsonSerializer.Serialize(result, jsonOpts), "application/json", Encoding.UTF8);
});

// DEBUG-ONLY endpoints (controlled via Debug:Enable or DEBUG_ENABLE=true)
if (debugEnabled)
{
  // GET /debug/props-sample?limit=10&zone=1&q=zona+1
  app.MapGet("/debug/props-sample", async (HttpContext ctx, PropsTool propsTool, int? limit, int? zone, string? q, int? minBanos, bool? casasOnly) =>
  {
    var all = await propsTool.GetAllPropsAsync(ctx.RequestAborted);
    var l = Math.Clamp(limit ?? 10, 1, 100);

    IEnumerable<PropertyItem> query = all;
    if (zone.HasValue)
    {
      var pattern = $@"\bzona\s*0?{zone.Value}\b";
      query = query.Where(p => {
        var u = (p.ubicacion ?? string.Empty).ToLowerInvariant();
        var d = (p.proyecto?.direccion ?? string.Empty).ToLowerInvariant();
        return (u.Contains("zona") || d.Contains("zona")) &&
               (System.Text.RegularExpressions.Regex.IsMatch(u, pattern) || System.Text.RegularExpressions.Regex.IsMatch(d, pattern));
      });
    }
    if (!string.IsNullOrWhiteSpace(q))
    {
      var qq = q.ToLowerInvariant();
      query = query.Where(p => (p.ubicacion ?? string.Empty).ToLowerInvariant().Contains(qq)
                            || (p.proyecto?.direccion ?? string.Empty).ToLowerInvariant().Contains(qq)
                            || (p.tipo ?? string.Empty).ToLowerInvariant().Contains(qq)
                            || (p.clase_tipo ?? string.Empty).ToLowerInvariant().Contains(qq));
    }

    if (casasOnly == true)
    {
      query = query.Where(p => (p.clase_tipo ?? string.Empty).ToLowerInvariant().Contains("casa"));
    }

    if (minBanos.HasValue)
    {
      query = query.Where(p => ((p.BanosConTilde ?? p.BanosSinTilde) ?? 0) >= minBanos.Value);
    }

    var sample = query
      .Select(p => new { p.id, p.tipo, p.clase_tipo, p.propiedad, p.modelo, p.ubicacion, direccion = p.proyecto?.direccion, banos = p.BanosConTilde ?? p.BanosSinTilde })
      .Take(l)
      .ToList();

    return Results.Json(new { success = true, data = sample }, jsonOpts);
  });
}

app.Run();

// ===== Helpers, modelos y DTOs =====
static string Sanitize(string? s)
{
  if (string.IsNullOrEmpty(s)) return "";
  var trimmed = s.Trim();
  return Regex.Replace(trimmed, @"\p{C}+", ""); // quita control chars
}
static bool IsPromptInjection(string q)
{
  var bad = new[] { "ignore previous", "system:", "Bearer ", "sk-", "override", "jailbreak" };
  var x = q.ToLowerInvariant();
  return bad.Any(x.Contains);
}

public class Interaction {
  public string? Id { get; set; }
  public string UserId { get; set; } = "u-demo";
  public int? PropiedadId { get; set; }
  public string Pregunta { get; set; } = "";
  public string? Respuesta { get; set; }
  public string? Status { get; set; }
  public DateTime CreatedAt { get; set; }
}

public class InteractionStore {
  private readonly List<Interaction> _list = new();
  public void Add(Interaction i) => _list.Add(i);
  public IEnumerable<Interaction> List(string? userId) => _list.Where(x => userId == null || x.UserId == userId);
  public object Metrics() => new {
    counts = _list.GroupBy(x => x.Status ?? "pendiente").ToDictionary(g => g.Key!, g => g.Count()),
    total = _list.Count
  };
}

// DTOs NLQ
public record NlqRequest(string query, string? locale = null, int limit = 10, string? estado = "disponible");
public record NlqResponse(bool success, string answer, object? toolPayload, object? toolArgs, long latency_ms, string trace_id);

// DTOs del catálogo con soporte de tildes
public sealed class ApiResp { public bool success { get; set; } public List<PropertyItem> data { get; set; } = new(); }
public sealed class PropertyItem {
  public int id { get; set; }
  public string? propiedad { get; set; }
  public decimal? area { get; set; }
  public string? tipo { get; set; }
  public string? clase_tipo { get; set; }
  public string? modelo { get; set; }
  public string? ubicacion { get; set; }
  public string? estado { get; set; }
  public string? fin_de_obra { get; set; }
  public string? fase { get; set; }
  public string? bloqueo { get; set; }
  public decimal? precio { get; set; }
  public decimal? precio_sugerido { get; set; }
  public int? proyectos_id { get; set; }
  public int? habitaciones { get; set; }
  [JsonPropertyName("baños")] public decimal? BanosConTilde { get; set; }
  [JsonPropertyName("banos")] public decimal? BanosSinTilde { get; set; }
  public int? parqueos { get; set; }
  public decimal? m2construccion { get; set; }
  public decimal? largo { get; set; }
  public decimal? ancho { get; set; }
  [JsonPropertyName("año")] public int? AnoConTilde { get; set; }
  [JsonPropertyName("ano")] public int? AnoSinTilde { get; set; }
  public string? titulo { get; set; }
  public string? descripcion { get; set; }
  public string? detalles { get; set; }
  public string? descripcion_corta { get; set; }
  public string? caracteristicas { get; set; }
  public decimal? latitud { get; set; }
  public decimal? longitud { get; set; }
  public string? comision_referencia { get; set; }
  public string? comision_directa { get; set; }
  public string? created_at { get; set; }
  public string? updated_at { get; set; }
  public Proyecto? proyecto { get; set; }
  public List<ImagenItem>? imagenes { get; set; }
}
public sealed class Proyecto {
  public int id { get; set; }
  public string? nombre_proyecto { get; set; }
  public string? direccion { get; set; }
  public string? aprobacion12cuotas { get; set; }
  public string? tipo { get; set; }
  public string? ubicacion { get; set; }
  public string? estado { get; set; }
  public string? created_at { get; set; }
  public string? updated_at { get; set; }
}
public sealed class ImagenItem {
  public string? tipo { get; set; }
  public string? url { get; set; }
  public string? formato { get; set; }
}