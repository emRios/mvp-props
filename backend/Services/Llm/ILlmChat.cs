namespace Backend.Services.Llm;

public interface ILlmChat
{
  Task<LlmRunResult> RunAsync(string userQuestion, int defaultLimit, string? defaultEstado, string? locale, CancellationToken ct = default);
}

public record LlmRunResult(string Answer, object? ToolPayload, object? ToolArgs);