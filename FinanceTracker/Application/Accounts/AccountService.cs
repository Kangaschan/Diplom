using Application.Abstractions;
using Application.Auth;
using Domain.Accounts;
using Domain.Common;
using Domain.Transactions;
using Shared.Results;

namespace Application.Accounts;

public sealed class AccountService(IFinanceDbContext dbContext, IAuthService authService)
{
    public async Task<Result<IReadOnlyCollection<Account>>> GetListAsync(bool includeArchived = false, CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<IReadOnlyCollection<Account>>.Failure(user.Error);

        var query = dbContext.Accounts.Where(a => a.UserId == user.Value.Id);
        if (!includeArchived)
        {
            query = query.Where(a => !a.IsArchived);
        }

        return Result<IReadOnlyCollection<Account>>.Success(query.OrderBy(a => a.Name).ToList());
    }

    public async Task<Result<Account>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<Account>.Failure(user.Error);

        var account = dbContext.Accounts.FirstOrDefault(a => a.Id == id && a.UserId == user.Value.Id);
        return account is null
            ? Result<Account>.Failure(AppErrors.NotFound("Account not found."))
            : Result<Account>.Success(account);
    }

    public async Task<Result<Account>> CreateAsync(string name, string currencyCode, decimal initialBalance, CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<Account>.Failure(user.Error);

        var account = new Account
        {
            UserId = user.Value.Id,
            Name = name.Trim(),
            CurrencyCode = currencyCode.Trim().ToUpperInvariant(),
            CurrentBalance = initialBalance,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await dbContext.AddAsync(account, ct);
        await dbContext.SaveChangesAsync(ct);

        return Result<Account>.Success(account);
    }

    public async Task<Result<Account>> UpdateAsync(Guid id, string name, bool isArchived, decimal? goalAmount, DateTime? goalDeadline, CancellationToken ct = default)
    {
        var accountResult = await GetByIdAsync(id, ct);
        if (accountResult.IsFailure || accountResult.Value is null) return Result<Account>.Failure(accountResult.Error);

        var account = accountResult.Value;
        account.Name = name.Trim();
        account.IsArchived = isArchived;
        account.FinancialGoalAmount = goalAmount;
        account.FinancialGoalDeadline = goalDeadline;
        account.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);
        return Result<Account>.Success(account);
    }

    public async Task<Result> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var accountResult = await GetByIdAsync(id, ct);
        if (accountResult.IsFailure || accountResult.Value is null) return Result.Failure(accountResult.Error);

        accountResult.Value.IsArchived = true;
        accountResult.Value.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<Transfer>> TransferAsync(Guid fromAccountId, Guid toAccountId, decimal amount, string currencyCode, string? description, CancellationToken ct = default)
    {
        if (amount <= 0) return Result<Transfer>.Failure(AppErrors.Validation("Amount must be positive."));
        if (fromAccountId == toAccountId) return Result<Transfer>.Failure(AppErrors.Validation("Accounts must be different."));

        var fromResult = await GetByIdAsync(fromAccountId, ct);
        if (fromResult.IsFailure || fromResult.Value is null) return Result<Transfer>.Failure(fromResult.Error);

        var toResult = await GetByIdAsync(toAccountId, ct);
        if (toResult.IsFailure || toResult.Value is null) return Result<Transfer>.Failure(toResult.Error);

        var from = fromResult.Value;
        var to = toResult.Value;

        if (from.CurrentBalance < amount)
        {
            return Result<Transfer>.Failure(AppErrors.Validation("Insufficient funds."));
        }

        var now = DateTime.UtcNow;

        from.CurrentBalance -= amount;
        from.UpdatedAt = now;
        to.CurrentBalance += amount;
        to.UpdatedAt = now;

        var outgoing = new Transaction
        {
            UserId = from.UserId,
            AccountId = from.Id,
            Type = TransactionType.Transfer,
            Amount = amount,
            CurrencyCode = currencyCode,
            TransactionDate = now,
            Description = description,
            Source = TransactionSource.Transfer,
            Status = TransactionStatus.Posted,
            CreatedAt = now,
            UpdatedAt = now
        };

        var incoming = new Transaction
        {
            UserId = to.UserId,
            AccountId = to.Id,
            Type = TransactionType.Transfer,
            Amount = amount,
            CurrencyCode = currencyCode,
            TransactionDate = now,
            Description = description,
            Source = TransactionSource.Transfer,
            Status = TransactionStatus.Posted,
            CreatedAt = now,
            UpdatedAt = now
        };

        await dbContext.AddAsync(outgoing, ct);
        await dbContext.AddAsync(incoming, ct);

        var transfer = new Transfer
        {
            UserId = from.UserId,
            FromAccountId = from.Id,
            ToAccountId = to.Id,
            Amount = amount,
            CurrencyCode = currencyCode,
            TransferDate = now,
            Description = description,
            OutgoingTransactionId = outgoing.Id,
            IncomingTransactionId = incoming.Id,
            CreatedAt = now
        };

        await dbContext.AddAsync(transfer, ct);
        await dbContext.SaveChangesAsync(ct);

        return Result<Transfer>.Success(transfer);
    }
}
