using Application.Abstractions;
using Application.Auth;
using Domain.Categories;
using Domain.Common;
using Shared.Results;

namespace Application.Categories;

public enum CategoryStatsPeriod
{
    Week = 1,
    Month = 2,
    Year = 3
}

public sealed record CategoryExpenseStatsDto(
    Guid CategoryId,
    string CategoryName,
    decimal Amount,
    int TransactionsCount,
    string CurrencyCode,
    DateTime From,
    DateTime To);

public sealed class CategoryService(IFinanceDbContext dbContext, IAuthService authService)
{
    public async Task<Result<IReadOnlyCollection<Category>>> GetAvailableAsync(CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<IReadOnlyCollection<Category>>.Failure(user.Error);

        var categories = dbContext.Categories
            .Where(c => c.IsActive && (c.IsSystem || c.UserId == user.Value.Id))
            .OrderBy(c => c.Name)
            .ToList();

        return Result<IReadOnlyCollection<Category>>.Success(categories);
    }

    public async Task<Result<Category>> CreateAsync(string name, CategoryType type, CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<Category>.Failure(user.Error);

        var category = new Category
        {
            UserId = user.Value.Id,
            Name = name.Trim(),
            Type = type,
            IsSystem = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await dbContext.AddAsync(category, ct);
        await dbContext.SaveChangesAsync(ct);

        return Result<Category>.Success(category);
    }

    public async Task<Result<Category>> UpdateAsync(Guid id, string name, bool isActive, CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<Category>.Failure(user.Error);

        var category = dbContext.Categories.FirstOrDefault(c => c.Id == id && c.UserId == user.Value.Id);
        if (category is null) return Result<Category>.Failure(AppErrors.NotFound("Category not found."));

        category.Name = name.Trim();
        category.IsActive = isActive;
        category.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return Result<Category>.Success(category);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result.Failure(user.Error);

        var category = dbContext.Categories.FirstOrDefault(c => c.Id == id && c.UserId == user.Value.Id);
        if (category is null) return Result.Failure(AppErrors.NotFound("Category not found."));

        var isUsed = dbContext.Transactions.Any(t => t.CategoryId == category.Id);
        if (isUsed)
        {
            return Result.Failure(AppErrors.Conflict("Category is used by transactions."));
        }

        dbContext.Remove(category);
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyCollection<CategoryExpenseStatsDto>>> GetExpenseStatsAsync(CategoryStatsPeriod period, CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result<IReadOnlyCollection<CategoryExpenseStatsDto>>.Failure(user.Error);
        }

        var now = DateTime.UtcNow;
        var (from, to) = ResolvePeriod(period, now);

        var userAccounts = dbContext.Accounts
            .Where(account => account.UserId == user.Value.Id && !account.IsArchived)
            .ToDictionary(account => account.Id, account => account.CurrencyCode);

        var categories = dbContext.Categories
            .Where(category => category.IsActive && category.Type == CategoryType.Expense && (category.IsSystem || category.UserId == user.Value.Id))
            .OrderBy(category => category.Name)
            .ToList();

        var transactions = dbContext.Transactions
            .Where(transaction =>
                transaction.UserId == user.Value.Id
                && transaction.Type == TransactionType.Expense
                && transaction.Status == TransactionStatus.Posted
                && transaction.TransactionDate >= from
                && transaction.TransactionDate <= to
                && transaction.CategoryId != null)
            .ToList();

        var stats = categories
            .Select(category =>
            {
                var categoryTransactions = transactions
                    .Where(transaction => transaction.CategoryId == category.Id)
                    .ToList();

                decimal amount = 0;
                var currencyCode = "USD";
                if (categoryTransactions.Count > 0)
                {
                    var firstTransaction = categoryTransactions[0];
                    if (userAccounts.TryGetValue(firstTransaction.AccountId, out var accountCurrencyCode) && !string.IsNullOrWhiteSpace(accountCurrencyCode))
                    {
                        currencyCode = accountCurrencyCode.Trim().ToUpperInvariant();
                    }
                    else if (!string.IsNullOrWhiteSpace(firstTransaction.CurrencyCode))
                    {
                        currencyCode = firstTransaction.CurrencyCode.Trim().ToUpperInvariant();
                    }

                    amount = categoryTransactions.Sum(transaction => transaction.Amount);
                }

                return new CategoryExpenseStatsDto(
                    category.Id,
                    category.Name,
                    decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
                    categoryTransactions.Count,
                    currencyCode,
                    from,
                    to);
            })
            .ToList();

        return Result<IReadOnlyCollection<CategoryExpenseStatsDto>>.Success(stats);
    }

    private static (DateTime From, DateTime To) ResolvePeriod(CategoryStatsPeriod period, DateTime now)
    {
        var utcToday = now.Date;

        return period switch
        {
            CategoryStatsPeriod.Week => (utcToday.AddDays(-6), utcToday.AddDays(1).AddTicks(-1)),
            CategoryStatsPeriod.Month => (new DateTime(utcToday.Year, utcToday.Month, 1, 0, 0, 0, DateTimeKind.Utc), utcToday.AddDays(1).AddTicks(-1)),
            CategoryStatsPeriod.Year => (new DateTime(utcToday.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc), utcToday.AddDays(1).AddTicks(-1)),
            _ => (utcToday.AddDays(-6), utcToday.AddDays(1).AddTicks(-1))
        };
    }
}
