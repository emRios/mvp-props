namespace Backend.Services.Llm;

public record LlmRequest(string Question, PropertyContext? Context);
public record LlmResponse(string Answer);
public record PropertyContext(int? Id, decimal? Precio, int? Habitaciones, decimal? Banos, int? Parqueos, decimal? M2Construccion, string? Ubicacion);

public interface ILlmClient
{
  Task<LlmResponse> AskAsync(LlmRequest req, CancellationToken ct = default);
}