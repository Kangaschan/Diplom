namespace Services.ExchangeRates;

public sealed class ExchangeRateApiOptions
{
    public const string SectionName = "ExchangeRateApi";

    public string BaseUrl { get; set; } = "https://v6.exchangerate-api.com";
    public string ApiKey { get; set; } = string.Empty;
    public string BaseCurrencyCode { get; set; } = "USD";
    public int RefreshIntervalMinutes { get; set; } = 60;
}
