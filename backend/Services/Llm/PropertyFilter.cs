namespace Backend.Services.Llm;

using System.Text.Json.Serialization;

public class PropertyFilter
{
    [JsonPropertyName("estado")]
    public string? Estado { get; set; }

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    [JsonPropertyName("cursor")]
    public int? Cursor { get; set; }

    [JsonPropertyName("fields")]
    public string? Fields { get; set; }

    [JsonPropertyName("precio_min")]
    public decimal? PrecioMin { get; set; }

    [JsonPropertyName("precio_max")]
    public decimal? PrecioMax { get; set; }

    [JsonPropertyName("habitaciones_min")]
    public int? HabitacionesMin { get; set; }

    [JsonPropertyName("baños_min")]
    public decimal? BañosMin { get; set; }

    [JsonPropertyName("area_min")]
    public decimal? AreaMin { get; set; }

    [JsonPropertyName("tipo")]
    public string? Tipo { get; set; }
}
