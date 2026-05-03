using Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Services.ExchangeRates;

public sealed class DashboardCurrencyProvider : IDashboardCurrencyProvider
{
    private readonly ExchangeRateApiOptions _options;

    public DashboardCurrencyProvider(IOptions<ExchangeRateApiOptions> options)
    {
        _options = options.Value;
    }

    public string GetDashboardCurrencyCode()
    {
        return string.IsNullOrWhiteSpace(_options.BaseCurrencyCode)
            ? "USD"
            : _options.BaseCurrencyCode.Trim().ToUpperInvariant();
    }
}
