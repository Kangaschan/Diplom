using Application.Abstractions;
using Application.Auth;
using Domain.Budgets;
using Domain.Categories;
using Domain.Common;
using Domain.Notifications;
using Shared.Results;

namespace Application.Budgets;

public sealed record BudgetUsageDto(
    Guid BudgetId,
    Guid CategoryId,
    string CategoryName,
    Guid? AccountId,
    string? AccountName,
    decimal LimitAmount,
    decimal UsedAmount,
    decimal RemainingAmount,
    decimal PercentUsed,
    bool IsNearLimit,
    bool IsExceeded,
    string Status,
    string CurrencyCode,
    BudgetPeriodType PeriodType,
    DateTime StartDate,
    DateTime EndDate);

public sealed class BudgetService
{
    private const decimal WarningThresholdPercent = 80m;

    private readonly IFinanceDbContext _dbContext;
    private readonly IAuthService _authService;

    public BudgetService(IFinanceDbContext dbContext, IAuthService authService)
    {
        _dbContext = dbContext;
        _authService = authService;
    }

    public async Task<Result<IReadOnlyCollection<Budget>>> GetListAsync(CancellationToken ct = default)
    {
        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result<IReadOnlyCollection<Budget>>.Failure(user.Error);
        }

        var budgets = _dbContext.Budgets
            .Where(budget => budget.UserId == user.Value.Id)
            .OrderByDescending(budget => budget.CreatedAt)
            .ToList();

        return Result<IReadOnlyCollection<Budget>>.Success(budgets);
    }

    public async Task<Result<Budget>> CreateAsync(
        Guid categoryId,
        Guid? accountId,
        decimal limitAmount,
        string currencyCode,
        BudgetPeriodType periodType,
        DateTime startDate,
        DateTime endDate,
        CancellationToken ct = default)
    {
        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result<Budget>.Failure(user.Error);
        }

        var validation = ValidateBudgetInput(user.Value.Id, null, categoryId, accountId, limitAmount, currencyCode, startDate, endDate);
        if (validation.IsFailure)
        {
            return Result<Budget>.Failure(validation.Error);
        }

        var budget = new Budget
        {
            UserId = user.Value.Id,
            CategoryId = categoryId,
            AccountId = accountId,
            LimitAmount = decimal.Round(limitAmount, 2),
            CurrencyCode = currencyCode.Trim().ToUpperInvariant(),
            PeriodType = periodType,
            StartDate = startDate,
            EndDate = endDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _dbContext.AddAsync(budget, ct);
        await _dbContext.SaveChangesAsync(ct);
        await EvaluateBudgetNotificationsAsync(
            user.Value.Id,
            new Guid?[] { budget.CategoryId },
            budget.AccountId is null ? [] : new[] { budget.AccountId.Value },
            ct);

        return Result<Budget>.Success(budget);
    }

    public async Task<Result<Budget>> UpdateAsync(Guid id, decimal limitAmount, DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result<Budget>.Failure(user.Error);
        }

        var budget = _dbContext.Budgets.FirstOrDefault(entity => entity.Id == id && entity.UserId == user.Value.Id);
        if (budget is null)
        {
            return Result<Budget>.Failure(AppErrors.NotFound("Budget not found."));
        }

        var validation = ValidateBudgetInput(
            user.Value.Id,
            budget.Id,
            budget.CategoryId,
            budget.AccountId,
            limitAmount,
            budget.CurrencyCode,
            startDate,
            endDate);

        if (validation.IsFailure)
        {
            return Result<Budget>.Failure(validation.Error);
        }

        budget.LimitAmount = decimal.Round(limitAmount, 2);
        budget.StartDate = startDate;
        budget.EndDate = endDate;
        budget.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        await EvaluateBudgetNotificationsAsync(
            user.Value.Id,
            new Guid?[] { budget.CategoryId },
            budget.AccountId is null ? [] : new[] { budget.AccountId.Value },
            ct);

        return Result<Budget>.Success(budget);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result.Failure(user.Error);
        }

        var budget = _dbContext.Budgets.FirstOrDefault(entity => entity.Id == id && entity.UserId == user.Value.Id);
        if (budget is null)
        {
            return Result.Failure(AppErrors.NotFound("Budget not found."));
        }

        _dbContext.Remove(budget);
        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyCollection<BudgetUsageDto>>> GetUsageAsync(CancellationToken ct = default)
    {
        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result<IReadOnlyCollection<BudgetUsageDto>>.Failure(user.Error);
        }

        var usage = BuildBudgetUsage(user.Value.Id);
        return Result<IReadOnlyCollection<BudgetUsageDto>>.Success(usage);
    }

    public async Task EvaluateBudgetNotificationsAsync(Guid userId, IEnumerable<Guid?> categoryIds, IEnumerable<Guid> accountIds, CancellationToken ct = default)
    {
        var categoryIdSet = categoryIds
            .Where(categoryId => categoryId.HasValue)
            .Select(categoryId => categoryId!.Value)
            .Distinct()
            .ToHashSet();

        if (categoryIdSet.Count == 0)
        {
            return;
        }

        var accountIdSet = accountIds.Distinct().ToHashSet();
        var usages = BuildBudgetUsage(userId)
            .Where(usage => categoryIdSet.Contains(usage.CategoryId))
            .Where(usage => usage.AccountId is null || accountIdSet.Count == 0 || accountIdSet.Contains(usage.AccountId.Value))
            .ToList();

        foreach (var usage in usages)
        {
            if (usage.IsExceeded)
            {
                await CreateBudgetNotificationIfNeededAsync(
                    userId,
                    usage.BudgetId,
                    $"Budget exceeded: {usage.CategoryName}",
                    $"You spent {usage.UsedAmount:F2} {usage.CurrencyCode} out of {usage.LimitAmount:F2} {usage.CurrencyCode}.",
                    ct);
            }
            else if (usage.IsNearLimit)
            {
                await CreateBudgetNotificationIfNeededAsync(
                    userId,
                    usage.BudgetId,
                    $"Budget warning: {usage.CategoryName}",
                    $"Budget usage reached {usage.PercentUsed:F2}% ({usage.UsedAmount:F2} of {usage.LimitAmount:F2} {usage.CurrencyCode}).",
                    ct);
            }
        }
    }

    private Result ValidateBudgetInput(
        Guid userId,
        Guid? currentBudgetId,
        Guid categoryId,
        Guid? accountId,
        decimal limitAmount,
        string currencyCode,
        DateTime startDate,
        DateTime endDate)
    {
        if (limitAmount <= 0)
        {
            return Result.Failure(AppErrors.Validation("Limit amount must be positive."));
        }

        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3)
        {
            return Result.Failure(AppErrors.Validation("Currency code must contain exactly 3 letters."));
        }

        if (endDate < startDate)
        {
            return Result.Failure(AppErrors.Validation("End date must be greater than or equal to start date."));
        }

        var category = _dbContext.Categories.FirstOrDefault(entity =>
            entity.Id == categoryId
            && entity.IsActive
            && (entity.UserId == userId || entity.IsSystem));

        if (category is null)
        {
            return Result.Failure(AppErrors.Validation("Category not found."));
        }

        if (category.Type != CategoryType.Expense)
        {
            return Result.Failure(AppErrors.Validation("Budgets can be created only for expense categories."));
        }

        if (accountId is not null)
        {
            var account = _dbContext.Accounts.FirstOrDefault(entity =>
                entity.Id == accountId.Value
                && entity.UserId == userId
                && !entity.IsArchived);

            if (account is null)
            {
                return Result.Failure(AppErrors.Validation("Account not found."));
            }

            if (!string.Equals(account.CurrencyCode, currencyCode.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure(AppErrors.Validation("Budget currency must match the selected account currency."));
            }
        }

        var hasOverlap = _dbContext.Budgets.Any(entity =>
            entity.UserId == userId
            && entity.CategoryId == categoryId
            && entity.AccountId == accountId
            && entity.CurrencyCode == currencyCode.Trim().ToUpperInvariant()
            && (!currentBudgetId.HasValue || entity.Id != currentBudgetId.Value)
            && entity.StartDate <= endDate
            && entity.EndDate >= startDate);

        if (hasOverlap)
        {
            return Result.Failure(AppErrors.Conflict("An overlapping budget already exists for this category, account and period."));
        }

        return Result.Success();
    }

    private List<BudgetUsageDto> BuildBudgetUsage(Guid userId)
    {
        var budgets = _dbContext.Budgets
            .Where(budget => budget.UserId == userId)
            .OrderByDescending(budget => budget.CreatedAt)
            .ToList();

        var categories = _dbContext.Categories
            .Where(category => category.IsSystem || category.UserId == userId)
            .ToDictionary(category => category.Id, category => category);

        var accounts = _dbContext.Accounts
            .Where(account => account.UserId == userId)
            .ToDictionary(account => account.Id, account => account);

        var expenses = _dbContext.Transactions
            .Where(transaction =>
                transaction.UserId == userId
                && transaction.Type == TransactionType.Expense
                && transaction.Status == TransactionStatus.Posted)
            .ToList();

        var result = new List<BudgetUsageDto>(budgets.Count);

        foreach (var budget in budgets)
        {
            var usedAmount = expenses
                .Where(transaction => transaction.CategoryId == budget.CategoryId)
                .Where(transaction => transaction.TransactionDate >= budget.StartDate && transaction.TransactionDate <= budget.EndDate)
                .Where(transaction => budget.AccountId == null || transaction.AccountId == budget.AccountId.Value)
                .Where(transaction => string.Equals(transaction.CurrencyCode, budget.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                .Sum(transaction => transaction.Amount);

            var normalizedUsed = decimal.Round(usedAmount, 2);
            var percentUsed = budget.LimitAmount == 0
                ? 0
                : decimal.Round(normalizedUsed / budget.LimitAmount * 100, 2);
            var remainingAmount = decimal.Round(Math.Max(0, budget.LimitAmount - normalizedUsed), 2);
            var isExceeded = normalizedUsed > budget.LimitAmount;
            var isNearLimit = !isExceeded && percentUsed >= WarningThresholdPercent;
            var status = isExceeded ? "exceeded" : isNearLimit ? "warning" : "normal";

            categories.TryGetValue(budget.CategoryId, out var category);
            Domain.Accounts.Account? account = null;
            if (budget.AccountId.HasValue)
            {
                accounts.TryGetValue(budget.AccountId.Value, out account);
            }

            result.Add(new BudgetUsageDto(
                budget.Id,
                budget.CategoryId,
                category?.Name ?? "Category",
                budget.AccountId,
                account?.Name,
                budget.LimitAmount,
                normalizedUsed,
                remainingAmount,
                percentUsed,
                isNearLimit,
                isExceeded,
                status,
                budget.CurrencyCode,
                budget.PeriodType,
                budget.StartDate,
                budget.EndDate));
        }

        return result;
    }

    private async Task CreateBudgetNotificationIfNeededAsync(
        Guid userId,
        Guid budgetId,
        string title,
        string message,
        CancellationToken ct)
    {
        var alreadyExists = _dbContext.Notifications.Any(notification =>
            notification.UserId == userId
            && notification.Type == NotificationType.BudgetWarning
            && notification.RelatedEntityType == "budget"
            && notification.RelatedEntityId == budgetId
            && !notification.IsRead
            && notification.Title == title);

        if (alreadyExists)
        {
            return;
        }

        var notification = new Notification
        {
            UserId = userId,
            Type = NotificationType.BudgetWarning,
            Title = title,
            Message = message,
            RelatedEntityType = "budget",
            RelatedEntityId = budgetId,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.AddAsync(notification, ct);
        await _dbContext.SaveChangesAsync(ct);
    }
}
