using Application.Abstractions;
using Application.Auth;
using Application.Budgets;
using Application.Subscriptions;
using Domain.Categories;
using Domain.Common;
using Domain.RecurringPayments;
using Domain.Transactions;
using Shared.Results;

namespace Application.RecurringPayments;

public sealed record RecurringPaymentDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? AccountId,
    string? AccountName,
    Guid? CategoryId,
    string? CategoryName,
    TransactionType Type,
    decimal Amount,
    string CurrencyCode,
    string Frequency,
    DateTime? StartDate,
    DateTime? NextExecutionAt,
    DateTime? EndDate,
    DateTime? LastExecutedAt,
    bool IsActive);

public sealed class RecurringPaymentService
{
    private static readonly HashSet<string> AllowedFrequencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "daily",
        "weekly",
        "monthly",
        "yearly"
    };

    private readonly IFinanceDbContext _dbContext;
    private readonly IAuthService _authService;
    private readonly IPremiumAccessService _premiumAccessService;
    private readonly ICurrencyRateProvider _currencyRateProvider;
    private readonly BudgetService _budgetService;

    public RecurringPaymentService(
        IFinanceDbContext dbContext,
        IAuthService authService,
        IPremiumAccessService premiumAccessService,
        ICurrencyRateProvider currencyRateProvider,
        BudgetService budgetService)
    {
        _dbContext = dbContext;
        _authService = authService;
        _premiumAccessService = premiumAccessService;
        _currencyRateProvider = currencyRateProvider;
        _budgetService = budgetService;
    }

    public async Task<Result<IReadOnlyCollection<RecurringPaymentDto>>> GetListAsync(CancellationToken ct = default)
    {
        var userResult = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<IReadOnlyCollection<RecurringPaymentDto>>.Failure(userResult.Error);
        }

        var premium = _premiumAccessService.EnsurePremium(userResult.Value);
        if (premium.IsFailure)
        {
            return Result<IReadOnlyCollection<RecurringPaymentDto>>.Failure(premium.Error);
        }

        var recurringPayments = _dbContext.RecurringPayments
            .Where(item => item.UserId == userResult.Value.Id)
            .OrderBy(item => item.NextExecutionAt ?? DateTime.MaxValue)
            .ThenBy(item => item.Name)
            .ToList();

        return Result<IReadOnlyCollection<RecurringPaymentDto>>.Success(await MapRecurringPaymentsAsync(userResult.Value.Id, recurringPayments, ct));
    }

    public async Task<Result<RecurringPaymentDto>> CreateAsync(
        string name,
        string? description,
        Guid accountId,
        Guid? categoryId,
        TransactionType type,
        decimal amount,
        string currencyCode,
        string frequency,
        DateTime firstExecutionDate,
        DateTime? endDate,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<RecurringPaymentDto>.Failure(AppErrors.Validation("Name is required."));
        }

        var userResult = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<RecurringPaymentDto>.Failure(userResult.Error);
        }

        var premium = _premiumAccessService.EnsurePremium(userResult.Value);
        if (premium.IsFailure)
        {
            return Result<RecurringPaymentDto>.Failure(premium.Error);
        }

        var validationResult = ValidateRecurringPaymentInput(
            userResult.Value.Id,
            accountId,
            categoryId,
            type,
            amount,
            currencyCode,
            frequency,
            firstExecutionDate,
            endDate);

        if (validationResult.IsFailure)
        {
            return Result<RecurringPaymentDto>.Failure(validationResult.Error);
        }

        var recurringPayment = new RecurringPayment
        {
            UserId = userResult.Value.Id,
            AccountId = accountId,
            CategoryId = categoryId,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Type = type,
            EstimatedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
            CurrencyCode = currencyCode.Trim().ToUpperInvariant(),
            Frequency = NormalizeFrequency(frequency),
            StartDate = firstExecutionDate,
            NextExecutionAt = firstExecutionDate,
            EndDate = endDate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _dbContext.AddAsync(recurringPayment, ct);
        await _dbContext.SaveChangesAsync(ct);

        return Result<RecurringPaymentDto>.Success(await MapRecurringPaymentAsync(userResult.Value.Id, recurringPayment, ct));
    }

    public async Task<Result<RecurringPaymentDto>> UpdateAsync(
        Guid id,
        string name,
        string? description,
        Guid accountId,
        Guid? categoryId,
        TransactionType type,
        decimal amount,
        string currencyCode,
        string frequency,
        DateTime firstExecutionDate,
        DateTime? endDate,
        bool isActive,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<RecurringPaymentDto>.Failure(AppErrors.Validation("Name is required."));
        }

        var userResult = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<RecurringPaymentDto>.Failure(userResult.Error);
        }

        var premium = _premiumAccessService.EnsurePremium(userResult.Value);
        if (premium.IsFailure)
        {
            return Result<RecurringPaymentDto>.Failure(premium.Error);
        }

        var recurringPayment = _dbContext.RecurringPayments.FirstOrDefault(item => item.Id == id && item.UserId == userResult.Value.Id);
        if (recurringPayment is null)
        {
            return Result<RecurringPaymentDto>.Failure(AppErrors.NotFound("Recurring payment not found."));
        }

        var validationResult = ValidateRecurringPaymentInput(
            userResult.Value.Id,
            accountId,
            categoryId,
            type,
            amount,
            currencyCode,
            frequency,
            firstExecutionDate,
            endDate);

        if (validationResult.IsFailure)
        {
            return Result<RecurringPaymentDto>.Failure(validationResult.Error);
        }

        recurringPayment.Name = name.Trim();
        recurringPayment.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        recurringPayment.AccountId = accountId;
        recurringPayment.CategoryId = categoryId;
        recurringPayment.Type = type;
        recurringPayment.EstimatedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        recurringPayment.CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        recurringPayment.Frequency = NormalizeFrequency(frequency);
        recurringPayment.StartDate = firstExecutionDate;
        recurringPayment.EndDate = endDate;
        recurringPayment.IsActive = isActive;
        recurringPayment.UpdatedAt = DateTime.UtcNow;

        if (recurringPayment.LastDetectedAt is null)
        {
            recurringPayment.NextExecutionAt = firstExecutionDate;
        }
        else if (recurringPayment.NextExecutionAt is null || recurringPayment.NextExecutionAt < firstExecutionDate)
        {
            recurringPayment.NextExecutionAt = firstExecutionDate;
        }

        await _dbContext.SaveChangesAsync(ct);

        return Result<RecurringPaymentDto>.Success(await MapRecurringPaymentAsync(userResult.Value.Id, recurringPayment, ct));
    }

    public async Task<Result<RecurringPaymentDto>> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var userResult = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<RecurringPaymentDto>.Failure(userResult.Error);
        }

        var premium = _premiumAccessService.EnsurePremium(userResult.Value);
        if (premium.IsFailure)
        {
            return Result<RecurringPaymentDto>.Failure(premium.Error);
        }

        var recurringPayment = _dbContext.RecurringPayments.FirstOrDefault(item => item.Id == id && item.UserId == userResult.Value.Id);
        if (recurringPayment is null)
        {
            return Result<RecurringPaymentDto>.Failure(AppErrors.NotFound("Recurring payment not found."));
        }

        recurringPayment.IsActive = isActive;
        recurringPayment.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        return Result<RecurringPaymentDto>.Success(await MapRecurringPaymentAsync(userResult.Value.Id, recurringPayment, ct));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var userResult = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result.Failure(userResult.Error);
        }

        var premium = _premiumAccessService.EnsurePremium(userResult.Value);
        if (premium.IsFailure)
        {
            return Result.Failure(premium.Error);
        }

        var recurringPayment = _dbContext.RecurringPayments.FirstOrDefault(item => item.Id == id && item.UserId == userResult.Value.Id);
        if (recurringPayment is null)
        {
            return Result.Failure(AppErrors.NotFound("Recurring payment not found."));
        }

        _dbContext.Remove(recurringPayment);
        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task ProcessDuePaymentsAsync(CancellationToken ct = default)
    {
        var dueRecurringPayments = _dbContext.RecurringPayments
            .Where(item => item.IsActive && item.NextExecutionAt.HasValue && item.NextExecutionAt.Value <= DateTime.UtcNow)
            .OrderBy(item => item.NextExecutionAt)
            .ToList();

        foreach (var recurringPayment in dueRecurringPayments)
        {
            await ProcessRecurringPaymentAsync(recurringPayment, ct);
        }
    }

    private async Task ProcessRecurringPaymentAsync(RecurringPayment recurringPayment, CancellationToken ct)
    {
        var user = _dbContext.Users.FirstOrDefault(item => item.Id == recurringPayment.UserId);
        if (user is null)
        {
            recurringPayment.IsActive = false;
            recurringPayment.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return;
        }

        var premiumAccessResult = _premiumAccessService.EnsurePremium(user);
        if (premiumAccessResult.IsFailure)
        {
            return;
        }

        if (!recurringPayment.AccountId.HasValue || !recurringPayment.StartDate.HasValue || !recurringPayment.NextExecutionAt.HasValue)
        {
            recurringPayment.IsActive = false;
            recurringPayment.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return;
        }

        var account = _dbContext.Accounts.FirstOrDefault(item => item.Id == recurringPayment.AccountId.Value && item.UserId == recurringPayment.UserId && !item.IsArchived);
        if (account is null)
        {
            recurringPayment.IsActive = false;
            recurringPayment.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return;
        }

        if (recurringPayment.CategoryId.HasValue)
        {
            var categoryExists = _dbContext.Categories.Any(category =>
                category.Id == recurringPayment.CategoryId.Value &&
                category.IsActive &&
                (category.UserId == recurringPayment.UserId || category.IsSystem));

            if (!categoryExists)
            {
                recurringPayment.IsActive = false;
                recurringPayment.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(ct);
                return;
            }
        }

        var now = DateTime.UtcNow;
        var affectedCategoryIds = new HashSet<Guid>();

        while (recurringPayment.IsActive && recurringPayment.NextExecutionAt.HasValue && recurringPayment.NextExecutionAt.Value <= now)
        {
            var scheduledAt = recurringPayment.NextExecutionAt.Value;

            if (recurringPayment.EndDate.HasValue && scheduledAt > recurringPayment.EndDate.Value)
            {
                recurringPayment.IsActive = false;
                recurringPayment.UpdatedAt = DateTime.UtcNow;
                break;
            }

            var alreadyExists = _dbContext.Transactions.Any(transaction =>
                transaction.RecurringPaymentId == recurringPayment.Id &&
                transaction.TransactionDate == scheduledAt);

            if (!alreadyExists)
            {
                var normalizedAmountResult = await NormalizeAmountForAccountAsync(
                    recurringPayment.EstimatedAmount,
                    recurringPayment.CurrencyCode,
                    account.CurrencyCode,
                    ct);

                if (normalizedAmountResult.IsFailure)
                {
                    break;
                }

                var transaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    UserId = recurringPayment.UserId,
                    AccountId = account.Id,
                    CategoryId = recurringPayment.CategoryId,
                    RecurringPaymentId = recurringPayment.Id,
                    Type = recurringPayment.Type,
                    Amount = normalizedAmountResult.Value,
                    CurrencyCode = account.CurrencyCode.Trim().ToUpperInvariant(),
                    TransactionDate = scheduledAt,
                    Description = string.IsNullOrWhiteSpace(recurringPayment.Description) ? recurringPayment.Name : recurringPayment.Description,
                    Source = TransactionSource.Recurring,
                    Status = TransactionStatus.Posted,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                ApplyBalance(account, transaction);
                await _dbContext.AddAsync(transaction, ct);

                if (transaction.Type == TransactionType.Expense && transaction.CategoryId.HasValue)
                {
                    affectedCategoryIds.Add(transaction.CategoryId.Value);
                }
            }

            recurringPayment.LastDetectedAt = scheduledAt;
            recurringPayment.NextExecutionAt = CalculateNextExecutionAt(scheduledAt, recurringPayment.Frequency);
            recurringPayment.UpdatedAt = DateTime.UtcNow;

            if (recurringPayment.EndDate.HasValue && recurringPayment.NextExecutionAt > recurringPayment.EndDate.Value)
            {
                recurringPayment.IsActive = false;
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        if (affectedCategoryIds.Count > 0)
        {
            await _budgetService.EvaluateBudgetNotificationsAsync(
                recurringPayment.UserId,
                affectedCategoryIds.Cast<Guid?>(),
                [account.Id],
                ct);
        }
    }

    private Result ValidateRecurringPaymentInput(
        Guid userId,
        Guid accountId,
        Guid? categoryId,
        TransactionType type,
        decimal amount,
        string currencyCode,
        string frequency,
        DateTime firstExecutionDate,
        DateTime? endDate)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3)
        {
            return Result.Failure(AppErrors.Validation("Currency code must contain exactly 3 letters."));
        }

        if (firstExecutionDate == default)
        {
            return Result.Failure(AppErrors.Validation("First execution date is required."));
        }

        if (string.IsNullOrWhiteSpace(frequency) || !AllowedFrequencies.Contains(frequency))
        {
            return Result.Failure(AppErrors.Validation("Frequency must be daily, weekly, monthly or yearly."));
        }

        if (amount <= 0)
        {
            return Result.Failure(AppErrors.Validation("Amount must be positive."));
        }

        if (type != TransactionType.Income && type != TransactionType.Expense)
        {
            return Result.Failure(AppErrors.Validation("Only income and expense recurring transactions are supported."));
        }

        if (endDate.HasValue && endDate.Value < firstExecutionDate)
        {
            return Result.Failure(AppErrors.Validation("End date must be greater than or equal to the first execution date."));
        }

        var account = _dbContext.Accounts.FirstOrDefault(item => item.Id == accountId && item.UserId == userId && !item.IsArchived);
        if (account is null)
        {
            return Result.Failure(AppErrors.Validation("Account not found."));
        }

        if (categoryId.HasValue)
        {
            var category = _dbContext.Categories.FirstOrDefault(item =>
                item.Id == categoryId.Value &&
                item.IsActive &&
                (item.UserId == userId || item.IsSystem));

            if (category is null)
            {
                return Result.Failure(AppErrors.Validation("Category not found."));
            }

            if ((type == TransactionType.Expense && category.Type != CategoryType.Expense) ||
                (type == TransactionType.Income && category.Type != CategoryType.Income))
            {
                return Result.Failure(AppErrors.Validation("Category type does not match transaction type."));
            }
        }

        return Result.Success();
    }

    private Task<IReadOnlyCollection<RecurringPaymentDto>> MapRecurringPaymentsAsync(
        Guid userId,
        IReadOnlyCollection<RecurringPayment> recurringPayments,
        CancellationToken ct)
    {
        var accountNames = _dbContext.Accounts
            .Where(account => account.UserId == userId)
            .ToDictionary(account => account.Id, account => account.Name);

        var categoryNames = _dbContext.Categories
            .Where(category => category.UserId == userId || category.IsSystem)
            .ToDictionary(category => category.Id, category => category.Name);

        var items = new List<RecurringPaymentDto>(recurringPayments.Count);
        foreach (var recurringPayment in recurringPayments)
        {
            items.Add(MapRecurringPayment(recurringPayment, accountNames, categoryNames));
        }

        return Task.FromResult<IReadOnlyCollection<RecurringPaymentDto>>(items);
    }

    private Task<RecurringPaymentDto> MapRecurringPaymentAsync(
        Guid userId,
        RecurringPayment recurringPayment,
        CancellationToken ct)
    {
        var accountNames = _dbContext.Accounts
            .Where(account => account.UserId == userId)
            .ToDictionary(account => account.Id, account => account.Name);

        var categoryNames = _dbContext.Categories
            .Where(category => category.UserId == userId || category.IsSystem)
            .ToDictionary(category => category.Id, category => category.Name);

        return Task.FromResult(MapRecurringPayment(recurringPayment, accountNames, categoryNames));
    }

    private RecurringPaymentDto MapRecurringPayment(
        RecurringPayment recurringPayment,
        IReadOnlyDictionary<Guid, string> accountNames,
        IReadOnlyDictionary<Guid, string> categoryNames)
    {
        string? accountName = null;
        if (recurringPayment.AccountId.HasValue && accountNames.TryGetValue(recurringPayment.AccountId.Value, out var resolvedAccountName))
        {
            accountName = resolvedAccountName;
        }

        string? categoryName = null;
        if (recurringPayment.CategoryId.HasValue && categoryNames.TryGetValue(recurringPayment.CategoryId.Value, out var resolvedCategoryName))
        {
            categoryName = resolvedCategoryName;
        }

        return new RecurringPaymentDto(
            recurringPayment.Id,
            recurringPayment.Name,
            recurringPayment.Description,
            recurringPayment.AccountId,
            accountName,
            recurringPayment.CategoryId,
            categoryName,
            recurringPayment.Type,
            recurringPayment.EstimatedAmount,
            recurringPayment.CurrencyCode,
            recurringPayment.Frequency,
            recurringPayment.StartDate,
            recurringPayment.NextExecutionAt,
            recurringPayment.EndDate,
            recurringPayment.LastDetectedAt,
            recurringPayment.IsActive);
    }

    private async Task<Result<decimal>> NormalizeAmountForAccountAsync(
        decimal amount,
        string sourceCurrencyCode,
        string targetCurrencyCode,
        CancellationToken ct)
    {
        if (string.Equals(sourceCurrencyCode, targetCurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            return Result<decimal>.Success(decimal.Round(amount, 2, MidpointRounding.AwayFromZero));
        }

        var convertResult = await _currencyRateProvider.ConvertAsync(
            amount,
            sourceCurrencyCode.Trim().ToUpperInvariant(),
            targetCurrencyCode.Trim().ToUpperInvariant(),
            ct);

        if (convertResult.IsFailure)
        {
            return Result<decimal>.Failure(convertResult.Error);
        }

        return Result<decimal>.Success(convertResult.Value);
    }

    private static void ApplyBalance(Domain.Accounts.Account account, Transaction transaction)
    {
        if (transaction.Type == TransactionType.Income)
        {
            account.CurrentBalance += transaction.Amount;
        }
        else if (transaction.Type == TransactionType.Expense)
        {
            account.CurrentBalance -= transaction.Amount;
        }

        account.UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeFrequency(string frequency)
    {
        return frequency.Trim().ToLowerInvariant();
    }

    private static DateTime CalculateNextExecutionAt(DateTime from, string frequency)
    {
        var normalizedFrequency = NormalizeFrequency(frequency);

        return normalizedFrequency switch
        {
            "daily" => from.AddDays(1),
            "weekly" => from.AddDays(7),
            "monthly" => from.AddMonths(1),
            "yearly" => from.AddYears(1),
            _ => from.AddMonths(1)
        };
    }
}
