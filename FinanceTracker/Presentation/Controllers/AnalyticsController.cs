using Application.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;

namespace Presentation.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize]
public sealed class AnalyticsController : ControllerBase
{
    private readonly AnalyticsService _analyticsService;

    public AnalyticsController(AnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var result = await _analyticsService.GetDashboardAsync(from, to, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("expenses-by-category")]
    public async Task<IActionResult> ExpensesByCategory([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var result = await _analyticsService.GetExpensesByCategoryAsync(from, to, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("cash-flow")]
    public async Task<IActionResult> CashFlow(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] AnalyticsGrouping grouping,
        CancellationToken ct)
    {
        var result = await _analyticsService.GetCashFlowAsync(from, to, grouping, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("balance-history")]
    public async Task<IActionResult> BalanceHistory(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] AnalyticsGrouping grouping,
        CancellationToken ct)
    {
        var result = await _analyticsService.GetBalanceHistoryAsync(from, to, grouping, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("accounts-distribution")]
    public async Task<IActionResult> AccountsDistribution(CancellationToken ct)
    {
        var result = await _analyticsService.GetAccountsDistributionAsync(ct);
        return this.ToActionResult(result);
    }

    [HttpGet("recurring-payments")]
    public async Task<IActionResult> RecurringPayments([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var result = await _analyticsService.GetRecurringPaymentsSummaryAsync(from, to, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("premium/compare")]
    public async Task<IActionResult> Compare(
        [FromQuery] DateTime previousFrom,
        [FromQuery] DateTime previousTo,
        [FromQuery] DateTime currentFrom,
        [FromQuery] DateTime currentTo,
        CancellationToken ct)
    {
        var result = await _analyticsService.ComparePeriodsAsync(previousFrom, previousTo, currentFrom, currentTo, ct);
        return this.ToActionResult(result);
    }
}
