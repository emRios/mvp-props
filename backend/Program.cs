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

var cfg         = builder.Configuration;
var catalogUrl  = cfg["CatalogUrl"]  ?? "https://test.controldepropiedades.com/api/propiedades/miraiz";
var cacheSecs   = int.TryParse(cfg["CacheSeconds"], out var s) ? s : 90;
var apiKey      = cfg["x-api-key"] ?? Environment.GetEnvironmentVariable("BACK_API_KEY") ?? "EOh1Bt9a1aiwEOaCXkrzOxDOmgUNVMGSAeMStF6W";
var llmProvider = (cfg["LLM_PROVIDER"] ?? "mock").ToLowerInvariant();
var llmApiKey   = cfg["LLM_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
var llmModel    = cfg["LLM_MODEL"] ?? cfg["OpenAI:Model"] ?? "gpt-4o-mini";
var propsLiteUrl = cfg["PropsLiteBaseUrl"] ?? "https://test.controldepropiedades.com/api/propiedades/miraiz";
var propsLiteApiKey = cfg["PropsLiteApiKey"];
var debugEnabled = bool.TryParse(cfg["Debug:Enable"] ?? Environment.GetEnvironmentVariable("DEBUG_ENABLE"), out var de) && de;

var jsonOpts = new JsonSerializerOptions {
  PropertyNamingPolicy = null,
  WriteIndented = false,
  Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};

builder.Services.AddRateLimiter(options =>
{
  options.AddFixedWindowLimiter("nlq", opt =>
  {
    opt.Window = TimeSpan.FromMinutes(1);
    opt.PermitLimit = 30;
    opt.QueueLimit = 0;
  });
});

builder.Services.AddSingleton<PropsTool>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var cache = sp.GetRequiredService<IMemoryCache>();
  var url = config["PropsLiteBaseUrl"] ?? "https://test.controldepropiedades.com/api/propiedades/miraiz";
  var apiKeyLite = config["PropsLiteApiKey"];
  return new PropsTool(url, apiKeyLite, cache);
});

builder.Services.AddSingleton<ILlmClient>(sp =>
{
  var factory = sp.GetRequiredService<IHttpClientFactory>();
  return llmProvider == "openai" && !string.IsNullOrWhiteSpace(llmApiKey)
    ? new LlmOpenAi(factory.CreateClient(), llmApiKey, llmModel)
    : new LlmMock(factory.CreateClient(), propsLiteUrl);
});

builder.Services.AddSingleton<ILlmChat>(sp =>
{
  var factory = sp.GetRequiredService<IHttpClientFactory>();
  var propsTool = sp.GetRequiredService<PropsTool>();
  return llmProvider == "openai" && !string.IsNullOrWhiteSpace(llmApiKey)
    ? new LlmChat(factory.CreateClient(), llmApiKey, llmModel, propsTool)
    : new LlmChatMock(propsTool);
});

var app = builder.Build();

app.UseRateLimiter();

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

app.Use(async (ctx, next) => {
  ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
  ctx.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
  ctx.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";
  if (ctx.Request.Method == "OPTIONS") { ctx.Response.StatusCode = 204; return; }
  await next();
});

app.Use(async (ctx, next) => {
  var p = ctx.Request.Path.ToString();
  var protectedRoute = p.StartsWith("/metrics");
  if (!protectedRoute) { await next(); return; }
  var auth = ctx.Request.Headers.Authorization.ToString();
  if (auth == $"Bearer {apiKey}") { await next(); }
  else ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
});

var store = new InteractionStore();

app.MapGet("/properties", async (HttpContext ctx, IMemoryCache cache, IHttpClientFactory httpFactory) =>
{
  if (cache.TryGetValue("props", out object? cached) && cached is not null)
  {
    var cachedJson = JsonSerializer.Serialize(cached, jsonOpts);
    var etag = $"\"{cachedJson.GetHashCode():X}\"";
    
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
    using var req = new HttpRequestMessage(HttpMethod.Get, propsLiteUrl);
    if (!string.IsNullOrWhiteSpace(propsLiteApiKey)) req.Headers.Add("x-api-key", propsLiteApiKey);
    var response = await http.SendAsync(req);
    
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

app.MapGet("/interactions", () => Results.Json(store.List(), jsonOpts));

// Catálogo: utilidades de diagnóstico ligeras
app.MapGet("/api/catalog/summary", async (IMemoryCache cache, IHttpClientFactory httpFactory) =>
{
  var http = httpFactory.CreateClient();
  http.Timeout = TimeSpan.FromSeconds(5);

  ApiResp? api = null;
  try
  {
    using var req = new HttpRequestMessage(HttpMethod.Get, propsLiteUrl);
    if (!string.IsNullOrWhiteSpace(propsLiteApiKey)) req.Headers.Add("x-api-key", propsLiteApiKey);
    var response = await http.SendAsync(req);
    if (!response.IsSuccessStatusCode)
      return Results.Json(new { success = false, error = $"Catálogo remoto respondió {response.StatusCode}", source = propsLiteUrl });

    api = await response.Content.ReadFromJsonAsync<ApiResp>();
  }
  catch (Exception ex)
  {
    return Results.Json(new { success = false, error = ex.Message, source = propsLiteUrl });
  }

  var items = api?.data ?? new List<PropertyItem>();
  // Normalización mínima
  foreach (var p in items)
  {
    if (p.BanosSinTilde is not null && p.BanosConTilde is null) p.BanosConTilde = p.BanosSinTilde;
    if (p.AnoSinTilde is not null && p.AnoConTilde is null) p.AnoConTilde = p.AnoSinTilde;
  }

  var total = items.Count;
  var minId = items.Count > 0 ? items.Min(x => x.id) : (int?)null;
  var maxId = items.Count > 0 ? items.Max(x => x.id) : (int?)null;
  var estados = items
    .GroupBy(x => (x.estado ?? "").ToLowerInvariant())
    .ToDictionary(g => g.Key, g => g.Count());

  return Results.Json(new { success = true, source = propsLiteUrl, total, minId, maxId, estados }, jsonOpts);
});

app.MapPost("/api/catalog/refresh", async (IMemoryCache cache, IHttpClientFactory httpFactory) =>
{
  cache.Remove("props");
  cache.Remove("all_props");
  // Devolver el resumen inmediatamente después de limpiar caché
  var http = httpFactory.CreateClient();
  http.Timeout = TimeSpan.FromSeconds(5);
  try
  {
    using var req = new HttpRequestMessage(HttpMethod.Get, propsLiteUrl);
    if (!string.IsNullOrWhiteSpace(propsLiteApiKey)) req.Headers.Add("x-api-key", propsLiteApiKey);
    var response = await http.SendAsync(req);
    if (!response.IsSuccessStatusCode)
      return Results.Json(new { success = false, error = $"Catálogo remoto respondió {response.StatusCode}", source = propsLiteUrl });

    var api = await response.Content.ReadFromJsonAsync<ApiResp>();
    var items = api?.data ?? new List<PropertyItem>();
    var total = items.Count;
    var minId = items.Count > 0 ? items.Min(x => x.id) : (int?)null;
    var maxId = items.Count > 0 ? items.Max(x => x.id) : (int?)null;
    return Results.Json(new { success = true, source = propsLiteUrl, total, minId, maxId }, jsonOpts);
  }
  catch (Exception ex)
  {
    return Results.Json(new { success = false, error = ex.Message, source = propsLiteUrl });
  }
});

app.MapPost("/interactions", async (HttpContext ctx, Interaction input, ILlmClient llm, IHttpClientFactory httpFactory) =>
{
  Console.WriteLine($"POST /interactions - PropiedadId: {input.PropiedadId}, Pregunta: {input.Pregunta}");
  
  if (ctx.Request.ContentLength is > 4096)
    return Results.BadRequest(new { error = "Body demasiado grande" });

  input.Pregunta = Sanitize(input.Pregunta);
  
  if (string.IsNullOrWhiteSpace(input.Pregunta)) {
    Console.WriteLine($"Validación fallida - Pregunta vacía");
    return Results.BadRequest(new { error = "Campo requerido: pregunta" });
  }
  if (input.Pregunta.Length > 500)
    return Results.BadRequest(new { error = "Pregunta muy larga" });
  if (IsPromptInjection(input.Pregunta))
    return Results.BadRequest(new { error = "Contenido no permitido" });

  input.Id        = Guid.NewGuid().ToString();
  input.Status    = "pendiente";
  input.CreatedAt = DateTime.UtcNow;

  PropertyContext? propCtx = null;
  if (input.PropiedadId is not null) {
    var http = httpFactory.CreateClient();
    http.Timeout = TimeSpan.FromSeconds(5);
    
    try {
        using var reqProp = new HttpRequestMessage(HttpMethod.Get, propsLiteUrl);
        if (!string.IsNullOrWhiteSpace(propsLiteApiKey)) reqProp.Headers.Add("x-api-key", propsLiteApiKey);
        var response = await http.SendAsync(reqProp, ctx.RequestAborted);
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
    }
  }

  var resp = await llm.AskAsync(new LlmRequest(input.Pregunta, propCtx), ctx.RequestAborted);
  input.Respuesta = resp.Answer;
  input.Status    = "respondida";
  store.Add(input);
  return Results.Json(input, jsonOpts);
});

app.MapGet("/metrics/interactions", () => Results.Json(store.Metrics(), jsonOpts));

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

// GET helper para probar NLQ vía querystring (misma lógica que POST)
app.MapGet("/api/nlq", async (string query, int? limit, string? estado, string? locale, ILlmChat llm, ILoggerFactory lf, CancellationToken ct) =>
{
  var sw = System.Diagnostics.Stopwatch.StartNew();
  var trace = Guid.NewGuid().ToString("N");
  var q = (query ?? "").Trim();
  if (string.IsNullOrWhiteSpace(q))
    return Results.BadRequest(new { success = false, error = "query vacío", trace_id = trace });

  try
  {
    var r = await llm.RunAsync(q, Math.Clamp(limit ?? 10, 1, 100), estado, locale, ct);
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
    using var req = new HttpRequestMessage(HttpMethod.Get, propsLiteUrl);
    if (!string.IsNullOrWhiteSpace(propsLiteApiKey)) req.Headers.Add("x-api-key", propsLiteApiKey);
    var response = await http.SendAsync(req);
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

  foreach (var p in api.data)
  {
    if (p.BanosSinTilde is not null && p.BanosConTilde is null) p.BanosConTilde = p.BanosSinTilde;
    if (p.AnoSinTilde is not null && p.AnoConTilde is null) p.AnoConTilde = p.AnoSinTilde;
  }

  var filtered = api.data.AsEnumerable();
  if (!string.IsNullOrWhiteSpace(estado))
  {
    var estados = estado.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(e => e.Trim())
                        .Where(e => e.Length > 0)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (estados.Count > 0)
      filtered = filtered.Where(p => p.estado != null && estados.Contains(p.estado));
  }

  if (afterId.HasValue)
    filtered = filtered.Where(p => p.id > afterId.Value);

  var maxLimit = Math.Clamp(limit ?? 20, 1, 100);
  filtered = filtered.OrderBy(p => p.id);
  var items = filtered.Take(maxLimit).ToList();

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

app.MapGet("/api/propiedades/miraiz", async (
  HttpContext ctx,
  IMemoryCache cache,
  IHttpClientFactory httpFactory,
  string? estado,
  int? afterId,
  int? limit) =>
{
  var cacheKey = $"miraiz-official:{estado}:{afterId}:{limit}";

  if (cache.TryGetValue(cacheKey, out object? cached) && cached is not null)
    return Results.Text(JsonSerializer.Serialize(cached, jsonOpts), "application/json", Encoding.UTF8);

  var http = httpFactory.CreateClient();
  http.Timeout = TimeSpan.FromSeconds(5);

  ApiResp? api = null;
  try
  {
    using var req = new HttpRequestMessage(HttpMethod.Get, propsLiteUrl);
    if (!string.IsNullOrWhiteSpace(propsLiteApiKey)) req.Headers.Add("x-api-key", propsLiteApiKey);
    var response = await http.SendAsync(req);
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
    Console.WriteLine($"Error obteniendo catálogo (alias miraiz): {ex.Message}");
    var fallbackData = new { success = true, data = new object[0], cursor = (int?)null };
    cache.Set(cacheKey, fallbackData, TimeSpan.FromSeconds(cacheSecs));
    return Results.Text(JsonSerializer.Serialize(fallbackData, jsonOpts), "application/json", Encoding.UTF8);
  }

  if (api is null || api.data is null)
  {
    var fallbackData = new { success = true, data = new object[0], cursor = (int?)null };
    return Results.Text(JsonSerializer.Serialize(fallbackData, jsonOpts), "application/json", Encoding.UTF8);
  }

  foreach (var p in api.data)
  {
    if (p.BanosSinTilde is not null && p.BanosConTilde is null) p.BanosConTilde = p.BanosSinTilde;
    if (p.AnoSinTilde is not null && p.AnoConTilde is null) p.AnoConTilde = p.AnoSinTilde;
  }

  var filtered = api.data.AsEnumerable();
  if (!string.IsNullOrWhiteSpace(estado))
  {
    var estados = estado.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(e => e.Trim())
                        .Where(e => e.Length > 0)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (estados.Count > 0)
      filtered = filtered.Where(p => p.estado != null && estados.Contains(p.estado));
  }

  if (afterId.HasValue)
    filtered = filtered.Where(p => p.id > afterId.Value);

  var maxLimit = Math.Clamp(limit ?? 20, 1, 100);
  filtered = filtered.OrderBy(p => p.id);
  var items = filtered.Take(maxLimit).ToList();

  var nextCursor = items.Count == maxLimit ? items.Last().id : (int?)null;
  var result = new { success = true, data = items, cursor = nextCursor };
  cache.Set(cacheKey, result, TimeSpan.FromSeconds(cacheSecs));
  return Results.Text(JsonSerializer.Serialize(result, jsonOpts), "application/json", Encoding.UTF8);
});

if (debugEnabled)
{
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
  public int? PropiedadId { get; set; }
  public string Pregunta { get; set; } = "";
  public string? Respuesta { get; set; }
  public string? Status { get; set; }
  public DateTime CreatedAt { get; set; }
}

public class InteractionStore {
  private readonly List<Interaction> _list = new();
  public void Add(Interaction i) => _list.Add(i);
  public IEnumerable<Interaction> List() => _list;
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