using Shared.Results;

namespace Application.Abstractions;

public interface ICurrencyRateProvider
{
    Task<Result<decimal>> ConvertAsync(decimal amount, string fromCurrencyCode, string toCurrencyCode, CancellationToken ct = default);
}
