using Application.Abstractions;
using Application.Auth;
using Application.Budgets;
using Domain.Common;
using Domain.Transactions;
using Shared.Results;

namespace Application.Transactions;

public sealed class TransactionService
{
    private readonly IFinanceDbContext _dbContext;
    private readonly IAuthService _authService;
    private readonly BudgetService _budgetService;
    private readonly ICurrencyRateProvider _currencyRateProvider;

    public TransactionService(
        IFinanceDbContext dbContext,
        IAuthService authService,
        BudgetService budgetService,
        ICurrencyRateProvider currencyRateProvider)
    {
        _dbContext = dbContext;
        _authService = authService;
        _budgetService = budgetService;
        _currencyRateProvider = currencyRateProvider;
    }

    public async Task<Result<IReadOnlyCollection<Transaction>>> GetListAsync(
        DateTime? from,
        DateTime? to,
        Guid? accountId,
        Guid? categoryId,
        TransactionType? type,
        string? search,
        CancellationToken ct = default)
    {
        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result<IReadOnlyCollection<Transaction>>.Failure(user.Error);
        }

        var query = _dbContext.Transactions.Where(t => t.UserId == user.Value.Id);

        if (from is not null) query = query.Where(t => t.TransactionDate >= from.Value);
        if (to is not null) query = query.Where(t => t.TransactionDate <= to.Value);
        if (accountId is not null) query = query.Where(t => t.AccountId == accountId.Value);
        if (categoryId is not null) query = query.Where(t => t.CategoryId == categoryId.Value);
        if (type is not null) query = query.Where(t => t.Type == type.Value);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(t => t.Description != null && t.Description.Contains(search));

        return Result<IReadOnlyCollection<Transaction>>.Success(query.OrderByDescending(t => t.TransactionDate).ToList());
    }

    public async Task<Result<Transaction>> CreateAsync(
        Guid accountId,
        Guid? categoryId,
        TransactionType type,
        decimal amount,
        string currencyCode,
        decimal? manualRate,
        DateTime transactionDate,
        string? description,
        TransactionSource source,
        CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            return Result<Transaction>.Failure(AppErrors.Validation("Amount must be positive."));
        }

        if (manualRate is <= 0)
        {
            return Result<Transaction>.Failure(AppErrors.Validation("Manual rate must be positive."));
        }

        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result<Transaction>.Failure(user.Error);
        }

        var account = _dbContext.Accounts.FirstOrDefault(a => a.Id == accountId && a.UserId == user.Value.Id);
        if (account is null)
        {
            return Result<Transaction>.Failure(AppErrors.NotFound("Account not found."));
        }

        if (categoryId is not null)
        {
            var categoryValid = _dbContext.Categories.Any(c => c.Id == categoryId && (c.UserId == user.Value.Id || c.IsSystem));
            if (!categoryValid)
            {
                return Result<Transaction>.Failure(AppErrors.Validation("Invalid category."));
            }
        }

        var normalizedAmountResult = await NormalizeAmountForAccountAsync(amount, currencyCode, account.CurrencyCode, manualRate, ct);
        if (normalizedAmountResult.IsFailure)
        {
            return Result<Transaction>.Failure(normalizedAmountResult.Error);
        }

        var transaction = new Transaction
        {
            UserId = user.Value.Id,
            AccountId = accountId,
            CategoryId = categoryId,
            Type = type,
            Amount = normalizedAmountResult.Value,
            CurrencyCode = account.CurrencyCode.Trim().ToUpperInvariant(),
            TransactionDate = transactionDate,
            Description = description,
            Source = source,
            Status = TransactionStatus.Posted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        ApplyBalance(account, transaction, +1);

        await _dbContext.AddAsync(transaction, ct);
        await _dbContext.SaveChangesAsync(ct);
        await RecalculateBudgetStateIfNeededAsync(transaction, null, ct);

        return Result<Transaction>.Success(transaction);
    }

    public async Task<Result<Transaction>> UpdateAsync(
        Guid id,
        Guid accountId,
        Guid? categoryId,
        decimal amount,
        string currencyCode,
        decimal? manualRate,
        string? description,
        DateTime date,
        CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            return Result<Transaction>.Failure(AppErrors.Validation("Amount must be positive."));
        }

        if (manualRate is <= 0)
        {
            return Result<Transaction>.Failure(AppErrors.Validation("Manual rate must be positive."));
        }

        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result<Transaction>.Failure(user.Error);
        }

        var transaction = _dbContext.Transactions.FirstOrDefault(t => t.Id == id && t.UserId == user.Value.Id);
        if (transaction is null)
        {
            return Result<Transaction>.Failure(AppErrors.NotFound("Transaction not found."));
        }

        var previousCategoryId = transaction.CategoryId;
        var previousAccountId = transaction.AccountId;
        var previousType = transaction.Type;

        var oldAccount = _dbContext.Accounts.FirstOrDefault(a => a.Id == transaction.AccountId && a.UserId == user.Value.Id);
        if (oldAccount is null)
        {
            return Result<Transaction>.Failure(AppErrors.NotFound("Source account not found."));
        }

        ApplyBalance(oldAccount, transaction, -1);

        var newAccount = _dbContext.Accounts.FirstOrDefault(a => a.Id == accountId && a.UserId == user.Value.Id);
        if (newAccount is null)
        {
            return Result<Transaction>.Failure(AppErrors.NotFound("Account not found."));
        }

        if (categoryId is not null)
        {
            var categoryValid = _dbContext.Categories.Any(c => c.Id == categoryId && (c.UserId == user.Value.Id || c.IsSystem));
            if (!categoryValid)
            {
                return Result<Transaction>.Failure(AppErrors.Validation("Invalid category."));
            }
        }

        var normalizedAmountResult = await NormalizeAmountForAccountAsync(amount, currencyCode, newAccount.CurrencyCode, manualRate, ct);
        if (normalizedAmountResult.IsFailure)
        {
            return Result<Transaction>.Failure(normalizedAmountResult.Error);
        }

        transaction.AccountId = accountId;
        transaction.CategoryId = categoryId;
        transaction.Amount = normalizedAmountResult.Value;
        transaction.CurrencyCode = newAccount.CurrencyCode.Trim().ToUpperInvariant();
        transaction.Description = description;
        transaction.TransactionDate = date;
        transaction.UpdatedAt = DateTime.UtcNow;

        ApplyBalance(newAccount, transaction, +1);

        await _dbContext.SaveChangesAsync(ct);
        await RecalculateBudgetStateIfNeededAsync(
            transaction,
            previousType == TransactionType.Expense ? (previousCategoryId, previousAccountId) : null,
            ct);

        return Result<Transaction>.Success(transaction);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result.Failure(user.Error);
        }

        var transaction = _dbContext.Transactions.FirstOrDefault(t => t.Id == id && t.UserId == user.Value.Id);
        if (transaction is null)
        {
            return Result.Failure(AppErrors.NotFound("Transaction not found."));
        }

        var previousBudgetContext = transaction.Type == TransactionType.Expense
            ? (transaction.CategoryId, (Guid?)transaction.AccountId)
            : ((Guid?)null, (Guid?)null);

        var account = _dbContext.Accounts.FirstOrDefault(a => a.Id == transaction.AccountId && a.UserId == user.Value.Id);
        if (account is not null)
        {
            ApplyBalance(account, transaction, -1);
        }

        _dbContext.Remove(transaction);
        await _dbContext.SaveChangesAsync(ct);
        await RecalculateBudgetStateIfNeededAsync(null, previousBudgetContext, ct, user.Value.Id);

        return Result.Success();
    }

    private async Task RecalculateBudgetStateIfNeededAsync(
        Transaction? currentTransaction,
        (Guid? CategoryId, Guid? AccountId)? previousBudgetContext,
        CancellationToken ct,
        Guid? explicitUserId = null)
    {
        var currentIsExpense = currentTransaction?.Type == TransactionType.Expense;
        var categoryIds = new List<Guid?>();
        var accountIds = new List<Guid>();

        if (currentIsExpense && currentTransaction is not null)
        {
            categoryIds.Add(currentTransaction.CategoryId);
            accountIds.Add(currentTransaction.AccountId);
        }

        if (previousBudgetContext is not null)
        {
            categoryIds.Add(previousBudgetContext.Value.CategoryId);
            if (previousBudgetContext.Value.AccountId.HasValue)
            {
                accountIds.Add(previousBudgetContext.Value.AccountId.Value);
            }
        }

        if (categoryIds.Count == 0)
        {
            return;
        }

        var userId = explicitUserId ?? currentTransaction!.UserId;
        await _budgetService.EvaluateBudgetNotificationsAsync(userId, categoryIds, accountIds, ct);
    }

    private async Task<Result<decimal>> NormalizeAmountForAccountAsync(
        decimal amount,
        string transactionCurrencyCode,
        string accountCurrencyCode,
        decimal? manualRate,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(transactionCurrencyCode))
        {
            return Result<decimal>.Failure(AppErrors.Validation("Currency code is required."));
        }

        var sourceCurrency = transactionCurrencyCode.Trim().ToUpperInvariant();
        var targetCurrency = accountCurrencyCode.Trim().ToUpperInvariant();

        if (sourceCurrency == targetCurrency)
        {
            return Result<decimal>.Success(decimal.Round(amount, 2, MidpointRounding.AwayFromZero));
        }

        if (manualRate.HasValue)
        {
            return Result<decimal>.Success(decimal.Round(amount * manualRate.Value, 2, MidpointRounding.AwayFromZero));
        }

        var convertedAmountResult = await _currencyRateProvider.ConvertAsync(amount, sourceCurrency, targetCurrency, ct);
        if (convertedAmountResult.IsFailure)
        {
            return Result<decimal>.Failure(convertedAmountResult.Error);
        }

        return Result<decimal>.Success(convertedAmountResult.Value);
    }

    private static void ApplyBalance(Domain.Accounts.Account account, Transaction transaction, int sign)
    {
        if (transaction.Type == TransactionType.Income)
        {
            account.CurrentBalance += sign * transaction.Amount;
        }
        else if (transaction.Type == TransactionType.Expense)
        {
            account.CurrentBalance -= sign * transaction.Amount;
        }

        account.UpdatedAt = DateTime.UtcNow;
    }
}
