using System.Net.Http.Json;

namespace Backend.Services.Llm;

public sealed class LlmMock(HttpClient http, string catalogUrl) : ILlmClient
{
  public async Task<LlmResponse> AskAsync(LlmRequest req, CancellationToken ct = default)
  {
    var Q = (req.Question ?? "").ToLowerInvariant();
    var p = req.Context;

    if (p is null)
    {
      try {
        var json = await http.GetFromJsonAsync<ApiResp>(catalogUrl, ct);
        var total = json?.data?.Count ?? 0;
        return new($"Tengo {total} propiedades en catálogo. Puedo responder sobre precio, habitaciones, baños, m² y ubicación.");
      } catch {
        return new("Puedo responder sobre precio, habitaciones, baños, m² y ubicación de propiedades del catálogo.");
      }
    }

    if (Q.Contains("precio"))   return new(p.Precio is null         ? "No tengo ese dato en el catálogo." : $"El precio es {p.Precio}.");
    if (Q.Contains("habitac"))  return new(p.Habitaciones is null   ? "No tengo ese dato en el catálogo." : $"Tiene {p.Habitaciones} habitaciones.");
    if (Q.Contains("baño") || Q.Contains("banio") || Q.Contains("banos"))
                                return new(p.Banos is null          ? "No tengo ese dato en el catálogo." : $"Tiene {p.Banos} baños.");
    if (Q.Contains("parqueo"))  return new(p.Parqueos is null       ? "No tengo ese dato en el catálogo." : $"Tiene {p.Parqueos} parqueos.");
    if (Q.Contains("m2") || Q.Contains("metros"))
                                return new(p.M2Construccion is null ? "No tengo ese dato en el catálogo." : $"Área construida: {p.M2Construccion} m².");
    if (Q.Contains("ubic"))     return new(string.IsNullOrWhiteSpace(p.Ubicacion)
                                                ? "No tengo ese dato en el catálogo." : $"Ubicación: {p.Ubicacion}.");
    return new("No tengo ese dato en el catálogo.");
  }
}

file sealed class ApiResp { public bool success { get; set; } public List<Item> data { get; set; } = new(); }
file sealed class Item { public int id { get; set; } }