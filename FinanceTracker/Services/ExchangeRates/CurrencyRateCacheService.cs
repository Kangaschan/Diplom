using Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Results;
using System.Net.Http.Json;

namespace Services.ExchangeRates;

public sealed class CurrencyRateCacheService(
    HttpClient httpClient,
    IOptions<ExchangeRateApiOptions> options,
    ILogger<CurrencyRateCacheService> logger) : BackgroundService, ICurrencyRateProvider
{
    private readonly ExchangeRateApiOptions _options = options.Value;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private Dictionary<string, decimal> _rates = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastUpdatedUtc = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshRatesAsync(stoppingToken);

        var intervalMinutes = _options.RefreshIntervalMinutes <= 0 ? 60 : _options.RefreshIntervalMinutes;
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RefreshRatesAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        finally
        {
            timer.Dispose();
        }
    }

    public async Task<Result<decimal>> ConvertAsync(decimal amount, string fromCurrencyCode, string toCurrencyCode, CancellationToken ct = default)
    {
        if (amount < 0)
        {
            return Result<decimal>.Failure(AppErrors.Validation("Amount must be non-negative."));
        }

        if (string.IsNullOrWhiteSpace(fromCurrencyCode) || string.IsNullOrWhiteSpace(toCurrencyCode))
        {
            return Result<decimal>.Failure(AppErrors.Validation("Currency code is required."));
        }

        var from = fromCurrencyCode.Trim().ToUpperInvariant();
        var to = toCurrencyCode.Trim().ToUpperInvariant();
        if (from == to)
        {
            return Result<decimal>.Success(amount);
        }

        await EnsureRatesLoadedAsync(ct);

        if (!_rates.TryGetValue(from, out var fromRate))
        {
            return Result<decimal>.Failure(AppErrors.Validation($"Currency '{from}' is not supported."));
        }

        if (!_rates.TryGetValue(to, out var toRate))
        {
            return Result<decimal>.Failure(AppErrors.Validation($"Currency '{to}' is not supported."));
        }

        var amountInBase = amount / fromRate;
        var converted = amountInBase * toRate;
        return Result<decimal>.Success(decimal.Round(converted, 2, MidpointRounding.AwayFromZero));
    }

    private async Task EnsureRatesLoadedAsync(CancellationToken ct)
    {
        if (_rates.Count > 0)
        {
            return;
        }

        await RefreshRatesAsync(ct);
    }

    private async Task RefreshRatesAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            logger.LogWarning("ExchangeRateApi: API key is not configured. Currency conversion is unavailable.");
            return;
        }

        var baseCurrency = _options.BaseCurrencyCode.Trim().ToUpperInvariant();
        var requestPath = $"https://v6.exchangerate-api.com/v6/{_options.ApiKey}/latest/{baseCurrency}";

        await _sync.WaitAsync(ct);
        try
        {
            using var response = await httpClient.GetAsync(requestPath, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("ExchangeRateApi request failed with status code {StatusCode}.", response.StatusCode);
                return;
            }

            var payload = await response.Content.ReadFromJsonAsync<ExchangeRateApiResponse>(cancellationToken: ct);
            if (payload is null || !string.Equals(payload.Result, "success", StringComparison.OrdinalIgnoreCase) || payload.ConversionRates.Count == 0)
            {
                logger.LogWarning("ExchangeRateApi returned invalid payload.");
                return;
            }

            _rates = new Dictionary<string, decimal>(payload.ConversionRates, StringComparer.OrdinalIgnoreCase)
            {
                [baseCurrency] = 1m
            };
            _lastUpdatedUtc = DateTime.UtcNow;
            logger.LogInformation("Exchange rates updated. Rates count: {RatesCount}. Updated at {UpdatedAtUtc}.", _rates.Count, _lastUpdatedUtc);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to refresh exchange rates.");
        }
        finally
        {
            _sync.Release();
        }
    }
}
