using Application.Abstractions;
using Application.Analytics;
using Application.Auth;
using Application.Subscriptions;
using Domain.Common;
using Shared.Results;
using System.Globalization;
using System.Text;

namespace Application.Exports;

public sealed class ExportService
{
    private readonly IFinanceDbContext _dbContext;
    private readonly IAuthService _authService;
    private readonly IPremiumAccessService _premiumAccessService;
    private readonly AnalyticsService _analyticsService;
    private readonly ICurrencyRateProvider _currencyRateProvider;
    private readonly IDashboardCurrencyProvider _dashboardCurrencyProvider;

    public ExportService(
        IFinanceDbContext dbContext,
        IAuthService authService,
        IPremiumAccessService premiumAccessService,
        AnalyticsService analyticsService,
        ICurrencyRateProvider currencyRateProvider,
        IDashboardCurrencyProvider dashboardCurrencyProvider)
    {
        _dbContext = dbContext;
        _authService = authService;
        _premiumAccessService = premiumAccessService;
        _analyticsService = analyticsService;
        _currencyRateProvider = currencyRateProvider;
        _dashboardCurrencyProvider = dashboardCurrencyProvider;
    }

    public async Task<Result<string>> ExportTransactionsCsvAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result<string>.Failure(user.Error);
        }

        var premium = _premiumAccessService.EnsurePremium(user.Value);
        if (premium.IsFailure)
        {
            return Result<string>.Failure(premium.Error);
        }

        var rows = _dbContext.Transactions
            .Where(t => t.UserId == user.Value.Id && t.TransactionDate >= from && t.TransactionDate <= to)
            .OrderByDescending(t => t.TransactionDate)
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("id,date,type,amount,currency,description");
        foreach (var row in rows)
        {
            builder.AppendLine($"{row.Id},{row.TransactionDate:O},{row.Type},{row.Amount},{row.CurrencyCode},\"{row.Description?.Replace("\"", "'", StringComparison.Ordinal)}\"");
        }

        return Result<string>.Success(builder.ToString());
    }

    public async Task<Result<byte[]>> ExportAnalyticsPdfAsync(
        DateTime from,
        DateTime to,
        AnalyticsGrouping grouping,
        CancellationToken ct = default)
    {
        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result<byte[]>.Failure(user.Error);
        }

        var premium = _premiumAccessService.EnsurePremium(user.Value);
        if (premium.IsFailure)
        {
            return Result<byte[]>.Failure(premium.Error);
        }

        var dashboardResult = await _analyticsService.GetDashboardAsync(from, to, ct);
        if (dashboardResult.IsFailure || dashboardResult.Value is null)
        {
            return Result<byte[]>.Failure(dashboardResult.Error);
        }

        var categoriesResult = await _analyticsService.GetExpensesByCategoryAsync(from, to, ct);
        if (categoriesResult.IsFailure || categoriesResult.Value is null)
        {
            return Result<byte[]>.Failure(categoriesResult.Error);
        }

        var balanceHistoryResult = await _analyticsService.GetBalanceHistoryAsync(from, to, grouping, ct);
        if (balanceHistoryResult.IsFailure || balanceHistoryResult.Value is null)
        {
            return Result<byte[]>.Failure(balanceHistoryResult.Error);
        }

        var cashFlowResult = await _analyticsService.GetCashFlowAsync(from, to, grouping, ct);
        if (cashFlowResult.IsFailure || cashFlowResult.Value is null)
        {
            return Result<byte[]>.Failure(cashFlowResult.Error);
        }

        var recurringResult = await _analyticsService.GetRecurringPaymentsSummaryAsync(from, to, ct);
        if (recurringResult.IsFailure || recurringResult.Value is null)
        {
            return Result<byte[]>.Failure(recurringResult.Error);
        }

        var reportResult = await BuildReportAsync(
            user.Value.Id,
            from,
            to,
            dashboardResult.Value,
            categoriesResult.Value,
            balanceHistoryResult.Value,
            cashFlowResult.Value,
            recurringResult.Value,
            ct);

        if (reportResult.IsFailure || reportResult.Value is null)
        {
            return Result<byte[]>.Failure(reportResult.Error);
        }

        var renderer = new AnalyticsPdfRenderer();
        return Result<byte[]>.Success(renderer.Render(reportResult.Value));
    }

    private async Task<Result<AnalyticsPdfReport>> BuildReportAsync(
        Guid userId,
        DateTime from,
        DateTime to,
        DashboardAnalyticsDto dashboard,
        IReadOnlyCollection<CategoryAnalyticsDto> categories,
        IReadOnlyCollection<BalanceHistoryPointDto> balanceHistory,
        IReadOnlyCollection<CashFlowPointDto> cashFlow,
        RecurringPaymentsAnalyticsDto recurring,
        CancellationToken ct)
    {
        var month = new DateTime(to.Year, to.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var dashboardCurrencyCode = _dashboardCurrencyProvider.GetDashboardCurrencyCode();

        var accounts = _dbContext.Accounts
            .Where(item => item.UserId == userId)
            .ToDictionary(item => item.Id, item => item.Name);

        var categoriesMap = _dbContext.Categories
            .Where(item => item.UserId == userId || item.IsSystem)
            .ToDictionary(item => item.Id, item => item.Name);

        var historyTransactions = _dbContext.Transactions
            .Where(item =>
                item.UserId == userId &&
                item.Status == TransactionStatus.Posted &&
                item.TransactionDate >= from &&
                item.TransactionDate <= to)
            .OrderByDescending(item => item.TransactionDate)
            .Take(14)
            .ToList();

        var history = historyTransactions
            .Select(item => new AnalyticsHistoryItem(
                item.TransactionDate,
                string.IsNullOrWhiteSpace(item.Description) ? ResolveTypeLabel(item.Type) : item.Description!,
                accounts.TryGetValue(item.AccountId, out var accountName) ? accountName : "Счет",
                item.CategoryId.HasValue && categoriesMap.TryGetValue(item.CategoryId.Value, out var categoryName) ? categoryName : "Без категории",
                ResolveSourceLabel(item.Source),
                ResolveTypeLabel(item.Type),
                item.Amount,
                item.CurrencyCode))
            .ToList();

        var monthStart = month;
        var monthEnd = month.AddMonths(1).AddTicks(-1);

        var monthExpenses = _dbContext.Transactions
            .Where(item =>
                item.UserId == userId &&
                item.Status == TransactionStatus.Posted &&
                item.Type == TransactionType.Expense &&
                item.TransactionDate >= monthStart &&
                item.TransactionDate <= monthEnd)
            .ToList();

        var calendarExpenses = new Dictionary<DateTime, CalendarExpenseItem>();
        foreach (var group in monthExpenses.GroupBy(item => item.TransactionDate.Date))
        {
            decimal sum = 0m;
            foreach (var transaction in group)
            {
                var converted = await _currencyRateProvider.ConvertAsync(
                    transaction.Amount,
                    transaction.CurrencyCode,
                    dashboardCurrencyCode,
                    ct);

                if (converted.IsFailure)
                {
                    return Result<AnalyticsPdfReport>.Failure(converted.Error);
                }

                sum += converted.Value;
            }

            calendarExpenses[group.Key] = new CalendarExpenseItem(decimal.Round(sum, 2, MidpointRounding.AwayFromZero), dashboardCurrencyCode);
        }

        var recurringRules = _dbContext.RecurringPayments
            .Where(item => item.UserId == userId && item.IsActive)
            .OrderBy(item => item.Name)
            .ToList();

        var calendarRecurring = BuildRecurringCalendar(recurringRules, monthStart, monthEnd);

        return Result<AnalyticsPdfReport>.Success(new AnalyticsPdfReport(
            from,
            to,
            monthStart,
            dashboard,
            categories.ToList(),
            balanceHistory.ToList(),
            cashFlow.ToList(),
            recurring,
            history,
            calendarExpenses,
            calendarRecurring));
    }

    private static IReadOnlyDictionary<DateTime, IReadOnlyCollection<CalendarRecurringItem>> BuildRecurringCalendar(
        IReadOnlyCollection<Domain.RecurringPayments.RecurringPayment> rules,
        DateTime monthStart,
        DateTime monthEnd)
    {
        var result = new Dictionary<DateTime, List<CalendarRecurringItem>>();

        foreach (var rule in rules)
        {
            var seed = rule.NextExecutionAt ?? rule.StartDate;
            if (!seed.HasValue)
            {
                continue;
            }

            var current = seed.Value.Date;
            while (current < monthStart.Date)
            {
                current = Advance(current, rule.Frequency);
                if (rule.EndDate.HasValue && current > rule.EndDate.Value.Date)
                {
                    break;
                }
            }

            while (current <= monthEnd.Date)
            {
                if (rule.EndDate.HasValue && current > rule.EndDate.Value.Date)
                {
                    break;
                }

                result[current] ??= new List<CalendarRecurringItem>();
                result[current].Add(new CalendarRecurringItem(rule.Name, rule.Type, rule.EstimatedAmount, rule.CurrencyCode));
                current = Advance(current, rule.Frequency);
            }
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyCollection<CalendarRecurringItem>)pair.Value
                .OrderBy(item => item.Name, StringComparer.Create(new CultureInfo("ru-RU"), true))
                .ToList());
    }

    private static DateTime Advance(DateTime current, string frequency)
    {
        return frequency.ToLowerInvariant() switch
        {
            "daily" => current.AddDays(1),
            "weekly" => current.AddDays(7),
            "yearly" => current.AddYears(1),
            _ => current.AddMonths(1)
        };
    }

    private static string ResolveSourceLabel(TransactionSource source)
    {
        return source switch
        {
            TransactionSource.Receipt => "Чек",
            TransactionSource.Transfer => "Перевод",
            TransactionSource.Recurring => "Повторяющийся",
            _ => "Вручную"
        };
    }

    private static string ResolveTypeLabel(TransactionType type)
    {
        return type switch
        {
            TransactionType.Income => "Доход",
            TransactionType.Transfer => "Перевод",
            _ => "Расход"
        };
    }
}
