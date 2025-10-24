using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace Backend.Services.Llm;

public class LlmChatMock : ILlmChat
{
  private readonly PropsTool _propsTool;

  public LlmChatMock(PropsTool propsTool)
  {
    _propsTool = propsTool;
  }

  public async Task<LlmRunResult> RunAsync(string userQuestion, int defaultLimit, string? defaultEstado, string? locale, CancellationToken ct = default)
  {
    var filter = new PropertyFilter { Limit = defaultLimit, Estado = defaultEstado };

    if (userQuestion.Contains("vendido")) filter.Estado = "vendido";
    if (userQuestion.Contains("reservado")) filter.Estado = "reservado";
    if (userQuestion.Contains("casa")) filter.Tipo = "Casa";
    if (userQuestion.Contains("lote")) filter.Tipo = "Lote";
    
    var match = Regex.Match(userQuestion, @"(\d+)\s*habitaciones");
    if (match.Success) filter.HabitacionesMin = int.Parse(match.Groups[1].Value);

    match = Regex.Match(userQuestion, @"(\d+)\s*baños");
    if (match.Success) filter.BañosMin = int.Parse(match.Groups[1].Value);

    match = Regex.Match(userQuestion, @"precio menor a (\d+)");
    if (match.Success) filter.PrecioMax = decimal.Parse(match.Groups[1].Value);

    var allProps = await _propsTool.GetAllPropsAsync(ct);

    var filteredProps = allProps
      .Where(p => string.IsNullOrWhiteSpace(filter.Estado) || p.estado?.Equals(filter.Estado, StringComparison.OrdinalIgnoreCase) == true)
      .Where(p => !filter.PrecioMax.HasValue || p.precio <= filter.PrecioMax.Value)
      .Where(p => !filter.HabitacionesMin.HasValue || p.habitaciones >= filter.HabitacionesMin.Value)
      .Where(p => !filter.BañosMin.HasValue || (p.BanosConTilde ?? p.BanosSinTilde) >= filter.BañosMin.Value)
      .Where(p => string.IsNullOrWhiteSpace(filter.Tipo) || p.tipo?.Equals(filter.Tipo, StringComparison.OrdinalIgnoreCase) == true)
      .Take(filter.Limit ?? defaultLimit)
      .ToList();

    var answer = $"Mock: Se encontraron {filteredProps.Count} propiedades.";
    var toolPayload = new { success = true, data = filteredProps };

    return new LlmRunResult(answer, toolPayload, filter);
  }
}