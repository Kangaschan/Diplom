using System.Text.Json.Serialization;

namespace Services.ExchangeRates;

public sealed class ExchangeRateApiResponse
{
    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;

    [JsonPropertyName("conversion_rates")]
    public Dictionary<string, decimal> ConversionRates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
