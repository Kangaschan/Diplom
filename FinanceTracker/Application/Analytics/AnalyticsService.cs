using Application.Abstractions;
using Application.Auth;
using Application.Subscriptions;
using Domain.Common;
using Shared.Results;

namespace Application.Analytics;

public sealed record DashboardAnalyticsDto(decimal TotalIncome, decimal TotalExpense, decimal Net, int TransactionsCount);
public sealed record CategoryAnalyticsDto(Guid? CategoryId, decimal Amount);
public sealed record PremiumComparisonDto(decimal PreviousIncome, decimal CurrentIncome, decimal PreviousExpense, decimal CurrentExpense);

public sealed class AnalyticsService(
    IFinanceDbContext dbContext,
    IAuthService authService,
    IPremiumAccessService premiumAccessService)
{
    public async Task<Result<DashboardAnalyticsDto>> GetDashboardAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var userResult = await authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null) return Result<DashboardAnalyticsDto>.Failure(userResult.Error);

        var transactions = dbContext.Transactions
            .Where(t => t.UserId == userResult.Value.Id && t.TransactionDate >= from && t.TransactionDate <= to && t.Status == TransactionStatus.Posted)
            .ToList();

        var income = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        var expense = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

        return Result<DashboardAnalyticsDto>.Success(new DashboardAnalyticsDto(income, expense, income - expense, transactions.Count));
    }

    public async Task<Result<IReadOnlyCollection<CategoryAnalyticsDto>>> GetExpensesByCategoryAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var userResult = await authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null) return Result<IReadOnlyCollection<CategoryAnalyticsDto>>.Failure(userResult.Error);

        var data = dbContext.Transactions
            .Where(t => t.UserId == userResult.Value.Id && t.Type == TransactionType.Expense && t.TransactionDate >= from && t.TransactionDate <= to)
            .GroupBy(t => t.CategoryId)
            .Select(g => new CategoryAnalyticsDto(g.Key, g.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Amount)
            .ToList();

        return Result<IReadOnlyCollection<CategoryAnalyticsDto>>.Success(data);
    }

    public async Task<Result<PremiumComparisonDto>> ComparePeriodsAsync(DateTime previousFrom, DateTime previousTo, DateTime currentFrom, DateTime currentTo, CancellationToken ct = default)
    {
        var userResult = await authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null) return Result<PremiumComparisonDto>.Failure(userResult.Error);

        var premiumResult = premiumAccessService.EnsurePremium(userResult.Value);
        if (premiumResult.IsFailure)
        {
            return Result<PremiumComparisonDto>.Failure(premiumResult.Error);
        }

        var tx = dbContext.Transactions.Where(t => t.UserId == userResult.Value.Id).ToList();

        var previous = tx.Where(t => t.TransactionDate >= previousFrom && t.TransactionDate <= previousTo).ToList();
        var current = tx.Where(t => t.TransactionDate >= currentFrom && t.TransactionDate <= currentTo).ToList();

        return Result<PremiumComparisonDto>.Success(new PremiumComparisonDto(
            previous.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount),
            current.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount),
            previous.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount),
            current.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount)
        ));
    }
}
