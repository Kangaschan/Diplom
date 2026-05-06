using Application.Abstractions;
using Application.Auth;
using Application.Subscriptions;
using Domain.Common;
using Shared.Results;

namespace Application.Analytics;

public enum AnalyticsGrouping
{
    Day = 1,
    Week = 2,
    Month = 3
}

public sealed record DashboardAnalyticsDto(
    decimal TotalIncome,
    decimal TotalExpense,
    decimal Net,
    int TransactionsCount,
    decimal TotalBalance,
    string CurrencyCode);

public sealed record CategoryAnalyticsDto(
    Guid? CategoryId,
    string CategoryName,
    decimal Amount,
    int TransactionsCount,
    string CurrencyCode);

public sealed record CashFlowPointDto(
    DateTime PeriodStart,
    string Label,
    decimal Income,
    decimal Expense,
    decimal Net,
    int TransactionsCount,
    string CurrencyCode);

public sealed record BalanceHistoryPointDto(
    DateTime PointDate,
    string Label,
    decimal Balance,
    string CurrencyCode);

public sealed record AccountDistributionDto(
    Guid AccountId,
    string AccountName,
    decimal Balance,
    string CurrencyCode,
    decimal SharePercent);

public sealed record RecurringPaymentAnalyticsItemDto(
    Guid RecurringPaymentId,
    string Name,
    TransactionType Type,
    string Frequency,
    bool IsActive,
    DateTime? NextExecutionAt,
    decimal RuleAmount,
    string RuleCurrencyCode,
    decimal GeneratedAmount,
    int ExecutionsCount,
    string CurrencyCode,
    string? AccountName);

public sealed record RecurringPaymentsAnalyticsDto(
    int ActiveRulesCount,
    int TotalRulesCount,
    int GeneratedTransactionsCount,
    decimal GeneratedIncome,
    decimal GeneratedExpense,
    string CurrencyCode,
    IReadOnlyCollection<RecurringPaymentAnalyticsItemDto> Items);

public sealed record PremiumComparisonDto(
    decimal PreviousIncome,
    decimal CurrentIncome,
    decimal PreviousExpense,
    decimal CurrentExpense,
    string CurrencyCode);

public sealed class AnalyticsService
{
    private readonly IFinanceDbContext _dbContext;
    private readonly IAuthService _authService;
    private readonly IPremiumAccessService _premiumAccessService;
    private readonly ICurrencyRateProvider _currencyRateProvider;
    private readonly IDashboardCurrencyProvider _dashboardCurrencyProvider;

    public AnalyticsService(
        IFinanceDbContext dbContext,
        IAuthService authService,
        IPremiumAccessService premiumAccessService,
        ICurrencyRateProvider currencyRateProvider,
        IDashboardCurrencyProvider dashboardCurrencyProvider)
    {
        _dbContext = dbContext;
        _authService = authService;
        _premiumAccessService = premiumAccessService;
        _currencyRateProvider = currencyRateProvider;
        _dashboardCurrencyProvider = dashboardCurrencyProvider;
    }

    public async Task<Result<DashboardAnalyticsDto>> GetDashboardAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var userResult = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<DashboardAnalyticsDto>.Failure(userResult.Error);
        }

        var dashboardCurrencyCode = _dashboardCurrencyProvider.GetDashboardCurrencyCode();
        var transactions = GetPostedTransactions(userResult.Value.Id, from, to);

        var normalizedIncomeResult = await SumTransactionsAsync(
            transactions.Where(transaction => transaction.Type == TransactionType.Income).ToList(),
            dashboardCurrencyCode,
            ct);

        if (normalizedIncomeResult.IsFailure)
        {
            return Result<DashboardAnalyticsDto>.Failure(normalizedIncomeResult.Error);
        }

        var normalizedExpenseResult = await SumTransactionsAsync(
            transactions.Where(transaction => transaction.Type == TransactionType.Expense).ToList(),
            dashboardCurrencyCode,
            ct);

        if (normalizedExpenseResult.IsFailure)
        {
            return Result<DashboardAnalyticsDto>.Failure(normalizedExpenseResult.Error);
        }

        var totalBalanceResult = await GetTotalBalanceAsync(userResult.Value.Id, dashboardCurrencyCode, ct);
        if (totalBalanceResult.IsFailure)
        {
            return Result<DashboardAnalyticsDto>.Failure(totalBalanceResult.Error);
        }

        return Result<DashboardAnalyticsDto>.Success(new DashboardAnalyticsDto(
            normalizedIncomeResult.Value,
            normalizedExpenseResult.Value,
            decimal.Round(normalizedIncomeResult.Value - normalizedExpenseResult.Value, 2, MidpointRounding.AwayFromZero),
            transactions.Count,
            totalBalanceResult.Value,
            dashboardCurrencyCode));
    }

    public async Task<Result<IReadOnlyCollection<CategoryAnalyticsDto>>> GetExpensesByCategoryAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var userResult = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<IReadOnlyCollection<CategoryAnalyticsDto>>.Failure(userResult.Error);
        }

        var dashboardCurrencyCode = _dashboardCurrencyProvider.GetDashboardCurrencyCode();
        var categories = _dbContext.Categories
            .Where(category => category.UserId == userResult.Value.Id || category.IsSystem)
            .ToDictionary(category => category.Id, category => category.Name);

        var expenses = GetPostedTransactions(userResult.Value.Id, from, to)
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .ToList();

        var groupedExpenses = expenses
            .GroupBy(transaction => transaction.CategoryId)
            .ToList();

        var result = new List<CategoryAnalyticsDto>(groupedExpenses.Count);

        foreach (var group in groupedExpenses)
        {
            var normalizedAmountResult = await SumTransactionsAsync(group.ToList(), dashboardCurrencyCode, ct);
            if (normalizedAmountResult.IsFailure)
            {
                return Result<IReadOnlyCollection<CategoryAnalyticsDto>>.Failure(normalizedAmountResult.Error);
            }

            var categoryName = group.Key.HasValue && categories.TryGetValue(group.Key.Value, out var resolvedCategoryName)
                ? resolvedCategoryName
                : "Others";

            result.Add(new CategoryAnalyticsDto(
                group.Key,
                categoryName,
                normalizedAmountResult.Value,
                group.Count(),
                dashboardCurrencyCode));
        }

        return Result<IReadOnlyCollection<CategoryAnalyticsDto>>.Success(
            result
                .OrderByDescending(item => item.Amount)
                .ToList());
    }

    public async Task<Result<IReadOnlyCollection<CashFlowPointDto>>> GetCashFlowAsync(
        DateTime from,
        DateTime to,
        AnalyticsGrouping grouping,
        CancellationToken ct = default)
    {
        var userResult = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<IReadOnlyCollection<CashFlowPointDto>>.Failure(userResult.Error);
        }

        var dashboardCurrencyCode = _dashboardCurrencyProvider.GetDashboardCurrencyCode();
        var transactions = GetPostedTransactions(userResult.Value.Id, from, to).ToList();

        var groupedTransactions = transactions
            .GroupBy(transaction => GetPeriodStart(transaction.TransactionDate, grouping))
            .OrderBy(group => group.Key)
            .ToList();

        var result = new List<CashFlowPointDto>(groupedTransactions.Count);

        foreach (var group in groupedTransactions)
        {
            var incomeResult = await SumTransactionsAsync(
                group.Where(transaction => transaction.Type == TransactionType.Income).ToList(),
                dashboardCurrencyCode,
                ct);

            if (incomeResult.IsFailure)
            {
                return Result<IReadOnlyCollection<CashFlowPointDto>>.Failure(incomeResult.Error);
            }

            var expenseResult = await SumTransactionsAsync(
                group.Where(transaction => transaction.Type == TransactionType.Expense).ToList(),
                dashboardCurrencyCode,
                ct);

            if (expenseResult.IsFailure)
            {
                return Result<IReadOnlyCollection<CashFlowPointDto>>.Failure(expenseResult.Error);
            }

            result.Add(new CashFlowPointDto(
                group.Key,
                BuildPeriodLabel(group.Key, grouping),
                incomeResult.Value,
                expenseResult.Value,
                decimal.Round(incomeResult.Value - expenseResult.Value, 2, MidpointRounding.AwayFromZero),
                group.Count(),
                dashboardCurrencyCode));
        }

        return Result<IReadOnlyCollection<CashFlowPointDto>>.Success(result);
    }

    public async Task<Result<IReadOnlyCollection<AccountDistributionDto>>> GetAccountsDistributionAsync(CancellationToken ct = default)
    {
        var userResult = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<IReadOnlyCollection<AccountDistributionDto>>.Failure(userResult.Error);
        }

        var dashboardCurrencyCode = _dashboardCurrencyProvider.GetDashboardCurrencyCode();
        var accounts = _dbContext.Accounts
            .Where(account => account.UserId == userResult.Value.Id && !account.IsArchived)
            .OrderBy(account => account.Name)
            .ToList();

        var normalizedBalances = new List<(Guid AccountId, string AccountName, decimal Balance)>(accounts.Count);

        foreach (var account in accounts)
        {
            var convertedBalanceResult = await _currencyRateProvider.ConvertAsync(
                account.CurrentBalance,
                account.CurrencyCode,
                dashboardCurrencyCode,
                ct);

            if (convertedBalanceResult.IsFailure)
            {
                return Result<IReadOnlyCollection<AccountDistributionDto>>.Failure(convertedBalanceResult.Error);
            }

            normalizedBalances.Add((
                account.Id,
                account.Name,
                decimal.Round(convertedBalanceResult.Value, 2, MidpointRounding.AwayFromZero)));
        }

        var totalBalance = normalizedBalances.Sum(item => item.Balance);
        var result = normalizedBalances
            .Select(item => new AccountDistributionDto(
                item.AccountId,
                item.AccountName,
                item.Balance,
                dashboardCurrencyCode,
                totalBalance == 0
                    ? 0
                    : decimal.Round(item.Balance / totalBalance * 100, 2, MidpointRounding.AwayFromZero)))
            .OrderByDescending(item => item.Balance)
            .ToList();

        return Result<IReadOnlyCollection<AccountDistributionDto>>.Success(result);
    }

    public async Task<Result<IReadOnlyCollection<BalanceHistoryPointDto>>> GetBalanceHistoryAsync(
        DateTime from,
        DateTime to,
        AnalyticsGrouping grouping,
        CancellationToken ct = default)
    {
        if (from > to)
        {
            return Result<IReadOnlyCollection<BalanceHistoryPointDto>>.Failure(
                new Shared.Errors.AppError("analytics.invalid_range", "The start date must be earlier than the end date."));
        }

        var userResult = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<IReadOnlyCollection<BalanceHistoryPointDto>>.Failure(userResult.Error);
        }

        var dashboardCurrencyCode = _dashboardCurrencyProvider.GetDashboardCurrencyCode();
        var currentBalanceResult = await GetTotalBalanceAsync(userResult.Value.Id, dashboardCurrencyCode, ct);
        if (currentBalanceResult.IsFailure)
        {
            return Result<IReadOnlyCollection<BalanceHistoryPointDto>>.Failure(currentBalanceResult.Error);
        }

        var transactionsFromPeriodStart = _dbContext.Transactions
            .Where(transaction =>
                transaction.UserId == userResult.Value.Id &&
                transaction.Status == TransactionStatus.Posted &&
                transaction.TransactionDate >= from)
            .ToList();

        var futureDeltaResult = await SumBalanceImpactAsync(transactionsFromPeriodStart, dashboardCurrencyCode, ct);
        if (futureDeltaResult.IsFailure)
        {
            return Result<IReadOnlyCollection<BalanceHistoryPointDto>>.Failure(futureDeltaResult.Error);
        }

        var openingBalance = decimal.Round(
            currentBalanceResult.Value - futureDeltaResult.Value,
            2,
            MidpointRounding.AwayFromZero);

        var inRangeTransactions = transactionsFromPeriodStart
            .Where(transaction => transaction.TransactionDate <= to)
            .ToList();

        var groupedTransactions = inRangeTransactions
            .GroupBy(transaction => GetPeriodStart(transaction.TransactionDate, grouping))
            .ToDictionary(group => group.Key, group => group.ToList());

        var result = new List<BalanceHistoryPointDto>();
        var runningBalance = openingBalance;

        foreach (var periodStart in EnumeratePeriods(from, to, grouping))
        {
            if (groupedTransactions.TryGetValue(periodStart, out var transactions))
            {
                var periodDeltaResult = await SumBalanceImpactAsync(transactions, dashboardCurrencyCode, ct);
                if (periodDeltaResult.IsFailure)
                {
                    return Result<IReadOnlyCollection<BalanceHistoryPointDto>>.Failure(periodDeltaResult.Error);
                }

                runningBalance = decimal.Round(
                    runningBalance + periodDeltaResult.Value,
                    2,
                    MidpointRounding.AwayFromZero);
            }

            result.Add(new BalanceHistoryPointDto(
                periodStart,
                BuildPeriodLabel(periodStart, grouping),
                runningBalance,
                dashboardCurrencyCode));
        }

        return Result<IReadOnlyCollection<BalanceHistoryPointDto>>.Success(result);
    }

    public async Task<Result<PremiumComparisonDto>> ComparePeriodsAsync(
        DateTime previousFrom,
        DateTime previousTo,
        DateTime currentFrom,
        DateTime currentTo,
        CancellationToken ct = default)
    {
        var userResult = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<PremiumComparisonDto>.Failure(userResult.Error);
        }

        var premiumResult = _premiumAccessService.EnsurePremium(userResult.Value);
        if (premiumResult.IsFailure)
        {
            return Result<PremiumComparisonDto>.Failure(premiumResult.Error);
        }

        var dashboardCurrencyCode = _dashboardCurrencyProvider.GetDashboardCurrencyCode();
        var transactions = _dbContext.Transactions
            .Where(transaction =>
                transaction.UserId == userResult.Value.Id &&
                transaction.Status == TransactionStatus.Posted)
            .ToList();

        var previousTransactions = transactions
            .Where(transaction => transaction.TransactionDate >= previousFrom && transaction.TransactionDate <= previousTo)
            .ToList();

        var currentTransactions = transactions
            .Where(transaction => transaction.TransactionDate >= currentFrom && transaction.TransactionDate <= currentTo)
            .ToList();

        var previousIncomeResult = await SumTransactionsAsync(
            previousTransactions.Where(transaction => transaction.Type == TransactionType.Income).ToList(),
            dashboardCurrencyCode,
            ct);

        if (previousIncomeResult.IsFailure)
        {
            return Result<PremiumComparisonDto>.Failure(previousIncomeResult.Error);
        }

        var currentIncomeResult = await SumTransactionsAsync(
            currentTransactions.Where(transaction => transaction.Type == TransactionType.Income).ToList(),
            dashboardCurrencyCode,
            ct);

        if (currentIncomeResult.IsFailure)
        {
            return Result<PremiumComparisonDto>.Failure(currentIncomeResult.Error);
        }

        var previousExpenseResult = await SumTransactionsAsync(
            previousTransactions.Where(transaction => transaction.Type == TransactionType.Expense).ToList(),
            dashboardCurrencyCode,
            ct);

        if (previousExpenseResult.IsFailure)
        {
            return Result<PremiumComparisonDto>.Failure(previousExpenseResult.Error);
        }

        var currentExpenseResult = await SumTransactionsAsync(
            currentTransactions.Where(transaction => transaction.Type == TransactionType.Expense).ToList(),
            dashboardCurrencyCode,
            ct);

        if (currentExpenseResult.IsFailure)
        {
            return Result<PremiumComparisonDto>.Failure(currentExpenseResult.Error);
        }

        return Result<PremiumComparisonDto>.Success(new PremiumComparisonDto(
            previousIncomeResult.Value,
            currentIncomeResult.Value,
            previousExpenseResult.Value,
            currentExpenseResult.Value,
            dashboardCurrencyCode));
    }

    public async Task<Result<RecurringPaymentsAnalyticsDto>> GetRecurringPaymentsSummaryAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        var userResult = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<RecurringPaymentsAnalyticsDto>.Failure(userResult.Error);
        }

        var dashboardCurrencyCode = _dashboardCurrencyProvider.GetDashboardCurrencyCode();
        var recurringPayments = _dbContext.RecurringPayments
            .Where(item => item.UserId == userResult.Value.Id)
            .OrderBy(item => item.Name)
            .ToList();

        var recurringPaymentIds = recurringPayments.Select(item => item.Id).ToHashSet();
        var recurringTransactions = _dbContext.Transactions
            .Where(transaction =>
                transaction.UserId == userResult.Value.Id &&
                transaction.RecurringPaymentId.HasValue &&
                recurringPaymentIds.Contains(transaction.RecurringPaymentId.Value) &&
                transaction.Status == TransactionStatus.Posted &&
                transaction.TransactionDate >= from &&
                transaction.TransactionDate <= to)
            .ToList();

        var accounts = _dbContext.Accounts
            .Where(account => account.UserId == userResult.Value.Id)
            .ToDictionary(account => account.Id, account => account.Name);

        var recurringItems = new List<RecurringPaymentAnalyticsItemDto>(recurringPayments.Count);

        foreach (var recurringPayment in recurringPayments)
        {
            var transactionsForRule = recurringTransactions
                .Where(transaction => transaction.RecurringPaymentId == recurringPayment.Id)
                .ToList();

            var generatedAmountResult = await SumTransactionsAsync(transactionsForRule, dashboardCurrencyCode, ct);
            if (generatedAmountResult.IsFailure)
            {
                return Result<RecurringPaymentsAnalyticsDto>.Failure(generatedAmountResult.Error);
            }

            recurringItems.Add(new RecurringPaymentAnalyticsItemDto(
                recurringPayment.Id,
                recurringPayment.Name,
                recurringPayment.Type,
                recurringPayment.Frequency,
                recurringPayment.IsActive,
                recurringPayment.NextExecutionAt,
                recurringPayment.EstimatedAmount,
                recurringPayment.CurrencyCode,
                generatedAmountResult.Value,
                transactionsForRule.Count,
                dashboardCurrencyCode,
                recurringPayment.AccountId.HasValue && accounts.TryGetValue(recurringPayment.AccountId.Value, out var accountName)
                    ? accountName
                    : null));
        }

        var generatedIncomeResult = await SumTransactionsAsync(
            recurringTransactions.Where(transaction => transaction.Type == TransactionType.Income).ToList(),
            dashboardCurrencyCode,
            ct);

        if (generatedIncomeResult.IsFailure)
        {
            return Result<RecurringPaymentsAnalyticsDto>.Failure(generatedIncomeResult.Error);
        }

        var generatedExpenseResult = await SumTransactionsAsync(
            recurringTransactions.Where(transaction => transaction.Type == TransactionType.Expense).ToList(),
            dashboardCurrencyCode,
            ct);

        if (generatedExpenseResult.IsFailure)
        {
            return Result<RecurringPaymentsAnalyticsDto>.Failure(generatedExpenseResult.Error);
        }

        return Result<RecurringPaymentsAnalyticsDto>.Success(new RecurringPaymentsAnalyticsDto(
            recurringPayments.Count(item => item.IsActive),
            recurringPayments.Count,
            recurringTransactions.Count,
            generatedIncomeResult.Value,
            generatedExpenseResult.Value,
            dashboardCurrencyCode,
            recurringItems
                .OrderByDescending(item => item.GeneratedAmount)
                .ThenBy(item => item.Name)
                .ToList()));
    }

    private List<Domain.Transactions.Transaction> GetPostedTransactions(Guid userId, DateTime from, DateTime to)
    {
        return _dbContext.Transactions
            .Where(transaction =>
                transaction.UserId == userId &&
                transaction.TransactionDate >= from &&
                transaction.TransactionDate <= to &&
                transaction.Status == TransactionStatus.Posted)
            .ToList();
    }

    private async Task<Result<decimal>> GetTotalBalanceAsync(Guid userId, string dashboardCurrencyCode, CancellationToken ct)
    {
        var accounts = _dbContext.Accounts
            .Where(account => account.UserId == userId && !account.IsArchived)
            .ToList();

        decimal totalBalance = 0m;

        foreach (var account in accounts)
        {
            var normalizedBalanceResult = await _currencyRateProvider.ConvertAsync(
                account.CurrentBalance,
                account.CurrencyCode,
                dashboardCurrencyCode,
                ct);

            if (normalizedBalanceResult.IsFailure)
            {
                return Result<decimal>.Failure(normalizedBalanceResult.Error);
            }

            totalBalance += normalizedBalanceResult.Value;
        }

        return Result<decimal>.Success(decimal.Round(totalBalance, 2, MidpointRounding.AwayFromZero));
    }

    private async Task<Result<decimal>> SumTransactionsAsync(
        IReadOnlyCollection<Domain.Transactions.Transaction> transactions,
        string targetCurrencyCode,
        CancellationToken ct)
    {
        decimal result = 0m;

        foreach (var transaction in transactions)
        {
            var convertedAmountResult = await _currencyRateProvider.ConvertAsync(
                transaction.Amount,
                transaction.CurrencyCode,
                targetCurrencyCode,
                ct);

            if (convertedAmountResult.IsFailure)
            {
                return Result<decimal>.Failure(convertedAmountResult.Error);
            }

            result += convertedAmountResult.Value;
        }

        return Result<decimal>.Success(decimal.Round(result, 2, MidpointRounding.AwayFromZero));
    }

    private async Task<Result<decimal>> SumBalanceImpactAsync(
        IReadOnlyCollection<Domain.Transactions.Transaction> transactions,
        string targetCurrencyCode,
        CancellationToken ct)
    {
        decimal result = 0m;

        foreach (var transaction in transactions)
        {
            if (transaction.Type == TransactionType.Transfer)
            {
                continue;
            }

            var convertedAmountResult = await _currencyRateProvider.ConvertAsync(
                transaction.Amount,
                transaction.CurrencyCode,
                targetCurrencyCode,
                ct);

            if (convertedAmountResult.IsFailure)
            {
                return Result<decimal>.Failure(convertedAmountResult.Error);
            }

            result += transaction.Type == TransactionType.Expense
                ? -convertedAmountResult.Value
                : convertedAmountResult.Value;
        }

        return Result<decimal>.Success(decimal.Round(result, 2, MidpointRounding.AwayFromZero));
    }

    private static DateTime GetPeriodStart(DateTime date, AnalyticsGrouping grouping)
    {
        var normalizedDate = date.Date;

        if (grouping == AnalyticsGrouping.Month)
        {
            return new DateTime(normalizedDate.Year, normalizedDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        if (grouping == AnalyticsGrouping.Week)
        {
            var delta = ((int)normalizedDate.DayOfWeek + 6) % 7;
            return normalizedDate.AddDays(-delta);
        }

        return normalizedDate;
    }

    private static string BuildPeriodLabel(DateTime periodStart, AnalyticsGrouping grouping)
    {
        if (grouping == AnalyticsGrouping.Month)
        {
            return periodStart.ToString("MM.yyyy");
        }

        if (grouping == AnalyticsGrouping.Week)
        {
            return $"{periodStart:dd.MM} - {periodStart.AddDays(6):dd.MM}";
        }

        return periodStart.ToString("dd.MM");
    }

    private static IReadOnlyCollection<DateTime> EnumeratePeriods(DateTime from, DateTime to, AnalyticsGrouping grouping)
    {
        var result = new List<DateTime>();
        var current = GetPeriodStart(from, grouping);
        var last = GetPeriodStart(to, grouping);

        while (current <= last)
        {
            result.Add(current);
            current = grouping switch
            {
                AnalyticsGrouping.Week => current.AddDays(7),
                AnalyticsGrouping.Month => current.AddMonths(1),
                _ => current.AddDays(1)
            };
        }

        return result;
    }
}
