using Application.Abstractions;
using Application.Auth;
using Domain.Common;
using Domain.Transactions;
using Shared.Results;

namespace Application.Transactions;

public sealed class TransactionService(IFinanceDbContext dbContext, IAuthService authService)
{
    public async Task<Result<IReadOnlyCollection<Transaction>>> GetListAsync(
        DateTime? from,
        DateTime? to,
        Guid? accountId,
        Guid? categoryId,
        TransactionType? type,
        string? search,
        CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<IReadOnlyCollection<Transaction>>.Failure(user.Error);

        var query = dbContext.Transactions.Where(t => t.UserId == user.Value.Id);

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
        DateTime transactionDate,
        string? description,
        TransactionSource source,
        CancellationToken ct = default)
    {
        if (amount <= 0) return Result<Transaction>.Failure(AppErrors.Validation("Amount must be positive."));

        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<Transaction>.Failure(user.Error);

        var account = dbContext.Accounts.FirstOrDefault(a => a.Id == accountId && a.UserId == user.Value.Id);
        if (account is null) return Result<Transaction>.Failure(AppErrors.NotFound("Account not found."));

        if (categoryId is not null)
        {
            var categoryValid = dbContext.Categories.Any(c => c.Id == categoryId && (c.UserId == user.Value.Id || c.IsSystem));
            if (!categoryValid) return Result<Transaction>.Failure(AppErrors.Validation("Invalid category."));
        }

        var transaction = new Transaction
        {
            UserId = user.Value.Id,
            AccountId = accountId,
            CategoryId = categoryId,
            Type = type,
            Amount = amount,
            CurrencyCode = currencyCode,
            TransactionDate = transactionDate,
            Description = description,
            Source = source,
            Status = TransactionStatus.Posted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        ApplyBalance(account, transaction, +1);

        await dbContext.AddAsync(transaction, ct);
        await dbContext.SaveChangesAsync(ct);

        return Result<Transaction>.Success(transaction);
    }

    public async Task<Result<Transaction>> UpdateAsync(Guid id, Guid accountId, Guid? categoryId, decimal amount, string? description, DateTime date, CancellationToken ct = default)
    {
        if (amount <= 0) return Result<Transaction>.Failure(AppErrors.Validation("Amount must be positive."));

        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<Transaction>.Failure(user.Error);

        var transaction = dbContext.Transactions.FirstOrDefault(t => t.Id == id && t.UserId == user.Value.Id);
        if (transaction is null) return Result<Transaction>.Failure(AppErrors.NotFound("Transaction not found."));

        var oldAccount = dbContext.Accounts.FirstOrDefault(a => a.Id == transaction.AccountId && a.UserId == user.Value.Id);
        if (oldAccount is null) return Result<Transaction>.Failure(AppErrors.NotFound("Source account not found."));

        ApplyBalance(oldAccount, transaction, -1);

        var newAccount = dbContext.Accounts.FirstOrDefault(a => a.Id == accountId && a.UserId == user.Value.Id);
        if (newAccount is null) return Result<Transaction>.Failure(AppErrors.NotFound("Account not found."));

        transaction.AccountId = accountId;
        transaction.CategoryId = categoryId;
        transaction.Amount = amount;
        transaction.Description = description;
        transaction.TransactionDate = date;
        transaction.UpdatedAt = DateTime.UtcNow;

        ApplyBalance(newAccount, transaction, +1);

        await dbContext.SaveChangesAsync(ct);
        return Result<Transaction>.Success(transaction);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result.Failure(user.Error);

        var transaction = dbContext.Transactions.FirstOrDefault(t => t.Id == id && t.UserId == user.Value.Id);
        if (transaction is null) return Result.Failure(AppErrors.NotFound("Transaction not found."));

        var account = dbContext.Accounts.FirstOrDefault(a => a.Id == transaction.AccountId && a.UserId == user.Value.Id);
        if (account is not null)
        {
            ApplyBalance(account, transaction, -1);
        }

        dbContext.Remove(transaction);
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
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
