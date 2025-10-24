using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Backend.Services.Llm;

public sealed class LlmOpenAi(HttpClient http, string apiKey, string model) : ILlmClient
{
  public async Task<LlmResponse> AskAsync(LlmRequest req, CancellationToken ct = default)
  {
    // Guardrail: SOLO contexto. Si falta un dato, decirlo.
    var sys = "Responde SOLO con los datos del contexto. Si falta un dato, di: 'No tengo ese dato en el catálogo.'";
    var ctx = req.Context is null ? "" :
      $"[contexto]\nprecio:{req.Context.Precio}\nhabitaciones:{req.Context.Habitaciones}\nbaños:{req.Context.Banos}\nparqueos:{req.Context.Parqueos}\nm2:{req.Context.M2Construccion}\nubicacion:{req.Context.Ubicacion}\n[/contexto]";

    using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
    msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    var payload = new {
      model,
      temperature = 0.1,
      messages = new object[] {
        new { role = "system", content = sys },
        new { role = "user", content = ctx + $"\nPregunta: {req.Question}" }
      }
    };
    msg.Content = JsonContent.Create(payload);
    var r = await http.SendAsync(msg, ct);
    if (!r.IsSuccessStatusCode) return new("No tengo ese dato en el catálogo.");
    using var s = await r.Content.ReadAsStreamAsync(ct);
    var json = await JsonDocument.ParseAsync(s, cancellationToken: ct);
    var answer = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
    return new(answer ?? "No tengo ese dato en el catálogo.");
  }
}