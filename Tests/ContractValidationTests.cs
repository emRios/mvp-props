using System.Net.Http.Json;
using System.Text.Json;
using Json.Schema;
using Xunit;

namespace backend.Tests;

public class ContractValidationTests
{
  private readonly HttpClient _httpClient;
  private readonly JsonSchema _schema;

  public ContractValidationTests()
  {
    _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5002") };
    
    // Cargar schema desde archivo
    var schemaPath = Path.Combine("..", "..", "..", "..", "Schemas", "miraiz.schema.json");
    var schemaJson = File.ReadAllText(schemaPath);
    _schema = JsonSchema.FromText(schemaJson);
  }

  [Fact]
  public async Task CatalogoMiraiz_DebeValidarContraSchema()
  {
    // Act: Consumir API del socio
    var response = await _httpClient.GetAsync("/api/propiedades/miraiz");
    response.EnsureSuccessStatusCode();

    var jsonString = await response.Content.ReadAsStringAsync();
    var jsonDoc = JsonDocument.Parse(jsonString);

    // Assert: Validar contra schema
    var validationResult = _schema.Evaluate(jsonDoc.RootElement);

    Assert.True(validationResult.IsValid, 
      $"El contrato del socio no cumple con el schema. Errores: {FormatErrors(validationResult)}");
  }

  [Fact]
  public async Task CatalogoMiraiz_DebeIncluirClavesConTilde()
  {
    // Act
    var response = await _httpClient.GetAsync("/api/propiedades/miraiz");
    response.EnsureSuccessStatusCode();

    var jsonString = await response.Content.ReadAsStringAsync();
    
    // Assert: Verificar que incluye claves acentuadas
    Assert.Contains("\"baños\"", jsonString);  // Con tilde
    Assert.Contains("\"año\"", jsonString);    // Con tilde
    Assert.Contains("\"imagenes\"", jsonString);
  }

  [Fact]
  public async Task CatalogoMiraiz_ImagenesDebenTenerFormatoEsperado()
  {
    // Act
    var response = await _httpClient.GetAsync("/api/propiedades/miraiz");
    response.EnsureSuccessStatusCode();

    using var jsonDoc = await response.Content.ReadFromJsonAsync<JsonDocument>();
    var data = jsonDoc!.RootElement.GetProperty("data");

    // Assert: Verificar estructura de imágenes
    if (data.GetArrayLength() > 0)
    {
      foreach (var item in data.EnumerateArray())
      {
        if (item.TryGetProperty("imagenes", out var imagenes) && imagenes.ValueKind == JsonValueKind.Array)
        {
          foreach (var imagen in imagenes.EnumerateArray())
          {
            Assert.True(imagen.TryGetProperty("tipo", out _), "Imagen debe tener 'tipo'");
            Assert.True(imagen.TryGetProperty("url", out _), "Imagen debe tener 'url'");
            Assert.True(imagen.TryGetProperty("formato", out _), "Imagen debe tener 'formato'");
          }
        }
      }
    }
  }

  private string FormatErrors(Json.Schema.EvaluationResults results)
  {
    if (results.IsValid) return string.Empty;

    var errors = new List<string>();
    CollectErrors(results, errors);
    return string.Join("; ", errors);
  }

  private void CollectErrors(Json.Schema.EvaluationResults results, List<string> errors)
  {
    if (!results.IsValid && results.Errors != null)
    {
      foreach (var (key, value) in results.Errors)
      {
        errors.Add($"{key}: {value}");
      }
    }

    if (results.Details != null)
    {
      foreach (var detail in results.Details)
      {
        CollectErrors(detail, errors);
      }
    }
  }
}