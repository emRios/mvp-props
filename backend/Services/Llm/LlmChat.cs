using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Services.Llm;

namespace Backend.Services.Llm;

public class LlmChat : ILlmChat
{
  private readonly HttpClient _http;
  private readonly string _apiKey;
  private readonly string _model;
  private readonly PropsTool _propsTool;
  private readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

  public LlmChat(HttpClient http, string apiKey, string model, PropsTool propsTool)
  {
    _http = http;
    _apiKey = apiKey;
    _model = model;
    _propsTool = propsTool;
  }

  public async Task<LlmRunResult> RunAsync(string userQuestion, int defaultLimit, string? defaultEstado, string? locale, CancellationToken ct = default)
  {
    var systemPrompt = @"Tu rol es convertir la pregunta de un usuario en un objeto de filtro JSON para una API de propiedades.
La pregunta es en lenguaje natural y debes extraer los parámetros de filtrado.
El JSON debe tener la siguiente estructura: { ""estado"": ""disponible|vendido|reservado"", ""limit"": number, ""fields"": ""string"", ""precio_min"": number, ""precio_max"": number, ""habitaciones_min"": number, ""baños_min"": number, ""area_min"": number, ""tipo"": ""Lote|Casa|Apartamento"" }.
- Si el usuario no especifica 'limit', usa 10.
- El campo 'estado' es opcional. Solo inclúyelo si el usuario lo pide explícitamente (ej: 'propiedades vendidas').
- En 'fields', incluye SIEMPRE 'id,propiedad,precio,imagenes.url,tipo,habitaciones,baños,area'.
- Extrae filtros numéricos como rangos (precio_min, precio_max) o mínimos (habitaciones_min).
- Responde únicamente con el objeto JSON. No incluyas texto adicional.
Ejemplo: 'casas con 3 cuartos y baratas' -> { ""tipo"": ""Casa"", ""habitaciones_min"": 3, ""precio_max"": 400000, ""limit"": 10, ""fields"": ""id,propiedad,precio,imagenes.url,tipo,habitaciones,baños,area"" }";

    var messages = new List<object>
    {
      new { role = "system", content = systemPrompt },
      new { role = "user", content = userQuestion }
    };

    var payload = new
    {
      model = _model,
      messages,
      response_format = new { type = "json_object" },
      temperature = 0.1
    };

    using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    req.Content = JsonContent.Create(payload);

    using var res = await _http.SendAsync(req, ct);
    res.EnsureSuccessStatusCode();

    var jsonResponse = await res.Content.ReadAsStringAsync(ct);
    var choice = JsonDocument.Parse(jsonResponse).RootElement.GetProperty("choices")[0];
    var filterJson = choice.GetProperty("message").GetProperty("content").GetString();

    PropertyFilter filter;
    try
    {
        filter = JsonSerializer.Deserialize<PropertyFilter>(filterJson ?? "{}", _json) ?? new PropertyFilter();
    }
    catch (JsonException)
    {
        // Si OpenAI devuelve un JSON inválido, usamos un filtro vacío por seguridad.
        filter = new PropertyFilter();
    }

    // Obtener todas las propiedades y aplicar el filtro localmente
    var allProps = await _propsTool.GetAllPropsAsync(ct);

  // Límite efectivo: priorizamos SIEMPRE el que viene en la request
  var limit = defaultLimit;

    // Intentar detectar patrón "zona <n>"
    var uq = userQuestion.ToLowerInvariant();
    var zoneMatch = Regex.Match(uq, @"\bzona\s*(\d{1,2})\b", RegexOptions.IgnoreCase);
    int? zoneNumber = zoneMatch.Success ? int.Parse(zoneMatch.Groups[1].Value) : (int?)null;

  // Palabras a ignorar (stopwords + tipos comunes)
    var stop = new HashSet<string>(new[] { "en","de","la","el","los","las","y","con","para","por","a","un","una","unos","unas","del","al","lo","su","sus","mi","mis","tu","tus","zona","barato","barata","baratos","baratas","muy" });
    var typeWords = new HashSet<string>(new[] { "casa","casas","terreno","terrenos","apartamento","apartamentos","lote","lotes" });

    // Extraer posibles palabras clave de ubicación (si no hay zona explícita)
    var locationKeywords = uq
      .Replace(",", " ")
      .Replace(".", " ")
      .Split(' ', StringSplitOptions.RemoveEmptyEntries)
      .Select(t => t.Trim())
      .Where(t => t.Length > 1 && !stop.Contains(t) && !typeWords.Contains(t))
      .ToList();

    // Si el LLM no detecta tipo, inferirlo de la pregunta (sinónimos básicos)
    string? inferredTipo = null;
    if (string.IsNullOrWhiteSpace(filter.Tipo))
    {
      if (Regex.IsMatch(uq, @"\b(casas?)\b", RegexOptions.IgnoreCase)) inferredTipo = "Casa";
      else if (Regex.IsMatch(uq, @"\b(apartamentos?)\b", RegexOptions.IgnoreCase)) inferredTipo = "Apartamento";
      else if (Regex.IsMatch(uq, @"\b(terrenos?|lotes?)\b", RegexOptions.IgnoreCase)) inferredTipo = "Terreno";
    }

    // Inferencias locales adicionales si el LLM no lo detecta
    // Habitaciones
    if (!filter.HabitacionesMin.HasValue)
    {
      var m = Regex.Match(uq, @"(\d{1,2})\s*(habitaciones?|cuartos?|dormitorios?)");
      if (m.Success && int.TryParse(m.Groups[1].Value, out var h)) filter.HabitacionesMin = h;
    }
    // Baños (con y sin tilde)
    if (!filter.BañosMin.HasValue)
    {
      var m = Regex.Match(uq, @"(\d{1,2})\s*(bañ(?:os|o)|ban(?:os|o))");
      if (m.Success && int.TryParse(m.Groups[1].Value, out var b)) filter.BañosMin = b;
    }
    // Área mínima (ej: '120 m2')
    if (!filter.AreaMin.HasValue)
    {
      var m = Regex.Match(uq, @"(\d{2,4})\s*(m2|metros|metros cuadrados)");
      if (m.Success && decimal.TryParse(m.Groups[1].Value, out var a)) filter.AreaMin = a;
    }

    // Parqueos mínimos (no existe en el filtro, se aplica directo)
    int? parqueosMin = null;
    {
      var m = Regex.Match(uq, @"(\d{1,2})\s*(parqueos?|estacionamientos?|garajes?|garage)");
      if (m.Success && int.TryParse(m.Groups[1].Value, out var pmin)) parqueosMin = pmin;
    }

    var filteredPropsQuery = allProps
      .Where(p => string.IsNullOrWhiteSpace(filter.Estado) || p.estado?.Equals(filter.Estado, StringComparison.OrdinalIgnoreCase) == true)
      .Where(p => !filter.PrecioMin.HasValue || (p.precio ?? 0) >= filter.PrecioMin.Value)
      .Where(p => !filter.PrecioMax.HasValue || (p.precio ?? 0) <= filter.PrecioMax.Value)
      .Where(p => !filter.HabitacionesMin.HasValue || (p.habitaciones ?? 0) >= filter.HabitacionesMin.Value)
      .Where(p => !filter.BañosMin.HasValue || ((p.BanosConTilde ?? p.BanosSinTilde) ?? 0) >= filter.BañosMin.Value)
      .Where(p => !filter.AreaMin.HasValue || (p.area ?? 0) >= filter.AreaMin.Value)
      .Where(p => !parqueosMin.HasValue || (p.parqueos ?? 0) >= parqueosMin.Value)
      // Tipo/clase_tipo (considerar también tipo del proyecto si existiera)
      .Where(p =>
        {
          var tipoQuery = !string.IsNullOrWhiteSpace(filter.Tipo) ? filter.Tipo : inferredTipo;
          if (string.IsNullOrWhiteSpace(tipoQuery)) return true;
          return (p.tipo?.IndexOf(tipoQuery, StringComparison.OrdinalIgnoreCase) >= 0)
              || (p.clase_tipo?.IndexOf(tipoQuery, StringComparison.OrdinalIgnoreCase) >= 0)
              || (p.proyecto?.tipo?.IndexOf(tipoQuery, StringComparison.OrdinalIgnoreCase) >= 0);
        }
      );

    // Filtro por ubicación con degradación: si no hay matches, no forzar
    List<PropertyItem> filteredProps;
    if (zoneNumber.HasValue)
    {
      var withZone = filteredPropsQuery.Where(p => {
        var u = p.ubicacion?.ToLowerInvariant() ?? string.Empty;
        var d = p.proyecto?.direccion?.ToLowerInvariant() ?? string.Empty;
        var pattern = $@"\bzona\s*0?{zoneNumber.Value}\b";
        return Regex.IsMatch(u, pattern) || Regex.IsMatch(d, pattern);
      });
      var list = withZone.Take(limit).ToList();
      filteredProps = list.Count > 0 ? list : filteredPropsQuery.Take(limit).ToList();
    }
    else if (locationKeywords.Any())
    {
      var withKeywords = filteredPropsQuery.Where(p => {
        var u = p.ubicacion?.ToLowerInvariant() ?? string.Empty;
        var d = p.proyecto?.direccion?.ToLowerInvariant() ?? string.Empty;
        return locationKeywords.Any(k => u.Contains(k) || d.Contains(k));
      });
      var list = withKeywords.Take(limit).ToList();
      filteredProps = list.Count > 0 ? list : filteredPropsQuery.Take(limit).ToList();
    }
    else
    {
      filteredProps = filteredPropsQuery.Take(limit).ToList();
    }

    var answer = locale?.ToLowerInvariant() == "en"
      ? $"Here are {filteredProps.Count} properties matching your search."
      : $"Aquí tienes {filteredProps.Count} propiedades que coinciden con tu búsqueda.";

    var toolPayload = new { success = true, data = filteredProps };

  // Reflejar el límite efectivo en el objeto devuelto (toolArgs)
  filter.Limit = limit;
    return new LlmRunResult(answer, toolPayload, filter);
  }
}