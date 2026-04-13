using Application.Abstractions;
using Application.Auth;
using Domain.Budgets;
using Domain.Common;
using Shared.Results;

namespace Application.Budgets;

public sealed record BudgetUsageDto(Guid BudgetId, decimal LimitAmount, decimal UsedAmount, decimal Percent, bool IsExceeded);

public sealed class BudgetService(IFinanceDbContext dbContext, IAuthService authService)
{
    public async Task<Result<IReadOnlyCollection<Budget>>> GetListAsync(CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<IReadOnlyCollection<Budget>>.Failure(user.Error);

        var budgets = dbContext.Budgets.Where(b => b.UserId == user.Value.Id).OrderByDescending(b => b.CreatedAt).ToList();
        return Result<IReadOnlyCollection<Budget>>.Success(budgets);
    }

    public async Task<Result<Budget>> CreateAsync(Guid categoryId, Guid? accountId, decimal limitAmount, string currencyCode, BudgetPeriodType periodType, DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        if (limitAmount <= 0) return Result<Budget>.Failure(AppErrors.Validation("Limit amount must be positive."));

        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<Budget>.Failure(user.Error);

        var budget = new Budget
        {
            UserId = user.Value.Id,
            CategoryId = categoryId,
            AccountId = accountId,
            LimitAmount = limitAmount,
            CurrencyCode = currencyCode,
            PeriodType = periodType,
            StartDate = startDate,
            EndDate = endDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await dbContext.AddAsync(budget, ct);
        await dbContext.SaveChangesAsync(ct);

        return Result<Budget>.Success(budget);
    }

    public async Task<Result<Budget>> UpdateAsync(Guid id, decimal limitAmount, DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<Budget>.Failure(user.Error);

        var budget = dbContext.Budgets.FirstOrDefault(b => b.Id == id && b.UserId == user.Value.Id);
        if (budget is null) return Result<Budget>.Failure(AppErrors.NotFound("Budget not found."));

        budget.LimitAmount = limitAmount;
        budget.StartDate = startDate;
        budget.EndDate = endDate;
        budget.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);
        return Result<Budget>.Success(budget);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result.Failure(user.Error);

        var budget = dbContext.Budgets.FirstOrDefault(b => b.Id == id && b.UserId == user.Value.Id);
        if (budget is null) return Result.Failure(AppErrors.NotFound("Budget not found."));

        dbContext.Remove(budget);
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyCollection<BudgetUsageDto>>> GetUsageAsync(CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<IReadOnlyCollection<BudgetUsageDto>>.Failure(user.Error);

        var expenses = dbContext.Transactions
            .Where(t => t.UserId == user.Value.Id && t.Type == TransactionType.Expense && t.Status == TransactionStatus.Posted)
            .ToList();

        var result = dbContext.Budgets
            .Where(b => b.UserId == user.Value.Id)
            .ToList()
            .Select(b =>
            {
                var used = expenses
                    .Where(t => t.CategoryId == b.CategoryId && t.TransactionDate >= b.StartDate && t.TransactionDate <= b.EndDate)
                    .Where(t => b.AccountId == null || t.AccountId == b.AccountId)
                    .Sum(t => t.Amount);

                var percent = b.LimitAmount == 0 ? 0 : decimal.Round(used / b.LimitAmount * 100, 2);
                return new BudgetUsageDto(b.Id, b.LimitAmount, used, percent, used > b.LimitAmount);
            })
            .ToList();

        return Result<IReadOnlyCollection<BudgetUsageDto>>.Success(result);
    }
}
