using Application.Abstractions;
using Application.Auth;
using Application.Subscriptions;
using Domain.CreditObligations;
using Shared.Results;

namespace Application.CreditObligations;

public sealed class CreditObligationService(IFinanceDbContext dbContext, IAuthService authService, IPremiumAccessService premiumAccessService)
{
    public async Task<Result<IReadOnlyCollection<CreditObligation>>> GetListAsync(CancellationToken ct = default)
    {
        var userResult = await authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null) return Result<IReadOnlyCollection<CreditObligation>>.Failure(userResult.Error);

        var premium = premiumAccessService.EnsurePremium(userResult.Value);
        if (premium.IsFailure) return Result<IReadOnlyCollection<CreditObligation>>.Failure(premium.Error);

        var items = dbContext.CreditObligations.Where(c => c.UserId == userResult.Value.Id).OrderByDescending(c => c.CreatedAt).ToList();
        return Result<IReadOnlyCollection<CreditObligation>>.Success(items);
    }

    public async Task<Result<CreditObligation>> CreateAsync(string name, decimal remainingAmount, decimal monthlyPayment, decimal interestRate, DateTime? endDate, CancellationToken ct = default)
    {
        var userResult = await authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null) return Result<CreditObligation>.Failure(userResult.Error);

        var premium = premiumAccessService.EnsurePremium(userResult.Value);
        if (premium.IsFailure) return Result<CreditObligation>.Failure(premium.Error);

        var item = new CreditObligation
        {
            UserId = userResult.Value.Id,
            Name = name,
            RemainingAmount = remainingAmount,
            MonthlyPayment = monthlyPayment,
            InterestRate = interestRate,
            EndDate = endDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await dbContext.AddAsync(item, ct);
        await dbContext.SaveChangesAsync(ct);

        return Result<CreditObligation>.Success(item);
    }
}
