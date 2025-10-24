using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Backend.Services;

public class PropsTool
{
  private readonly string _catalogUrl;
  private readonly IMemoryCache _cache;
  private readonly int _cacheSecs = 90; // Default cache duration
  private readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = null };
  private static readonly HttpClient _http = new HttpClient();

  public PropsTool(string catalogUrl, IMemoryCache cache)
  {
    _catalogUrl = catalogUrl;
    _cache = cache;
  }

  public async Task<List<PropertyItem>> GetAllPropsAsync(CancellationToken ct)
  {
    if (_cache.TryGetValue("all_props", out List<PropertyItem>? cachedProps) && cachedProps is not null)
    {
      return cachedProps;
    }

    using var req = new HttpRequestMessage(HttpMethod.Get, _catalogUrl);
    using var res = await _http.SendAsync(req, ct);
    
    if (!res.IsSuccessStatusCode)
    {
        // Lanzar una excepción clara si la API del catálogo falla
        throw new HttpRequestException($"Fallo al obtener el catálogo de propiedades. Status: {res.StatusCode}, URL: {_catalogUrl}");
    }

    var apiResp = await res.Content.ReadFromJsonAsync<ApiResp>(_json, ct);
    var props = apiResp?.data ?? new List<PropertyItem>();

    // Normalizar baños y año
    foreach (var p in props)
    {
      if (p.BanosSinTilde is not null && p.BanosConTilde is null) p.BanosConTilde = p.BanosSinTilde;
      if (p.AnoSinTilde is not null && p.AnoConTilde is null) p.AnoConTilde = p.AnoSinTilde;
    }

    _cache.Set("all_props", props, TimeSpan.FromSeconds(_cacheSecs));
    return props;
  }
}