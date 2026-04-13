using Application.Abstractions;
using Application.Auth;
using Application.Subscriptions;
using Domain.RecurringPayments;
using Shared.Results;

namespace Application.RecurringPayments;

public sealed class RecurringPaymentService(IFinanceDbContext dbContext, IAuthService authService, IPremiumAccessService premiumAccessService)
{
    public async Task<Result<IReadOnlyCollection<RecurringPayment>>> GetListAsync(CancellationToken ct = default)
    {
        var userResult = await authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null) return Result<IReadOnlyCollection<RecurringPayment>>.Failure(userResult.Error);

        var premium = premiumAccessService.EnsurePremium(userResult.Value);
        if (premium.IsFailure) return Result<IReadOnlyCollection<RecurringPayment>>.Failure(premium.Error);

        var items = dbContext.RecurringPayments.Where(r => r.UserId == userResult.Value.Id).OrderByDescending(r => r.CreatedAt).ToList();
        return Result<IReadOnlyCollection<RecurringPayment>>.Success(items);
    }

    public async Task<Result<RecurringPayment>> CreateAsync(string name, decimal estimatedAmount, string currencyCode, string frequency, CancellationToken ct = default)
    {
        var userResult = await authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null) return Result<RecurringPayment>.Failure(userResult.Error);

        var premium = premiumAccessService.EnsurePremium(userResult.Value);
        if (premium.IsFailure) return Result<RecurringPayment>.Failure(premium.Error);

        var item = new RecurringPayment
        {
            UserId = userResult.Value.Id,
            Name = name,
            EstimatedAmount = estimatedAmount,
            CurrencyCode = currencyCode,
            Frequency = frequency,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await dbContext.AddAsync(item, ct);
        await dbContext.SaveChangesAsync(ct);

        return Result<RecurringPayment>.Success(item);
    }
}
