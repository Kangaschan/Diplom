using Application.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;

namespace Presentation.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize]
public sealed class AnalyticsController(AnalyticsService analyticsService) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var result = await analyticsService.GetDashboardAsync(from, to, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("expenses-by-category")]
    public async Task<IActionResult> ExpensesByCategory([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var result = await analyticsService.GetExpensesByCategoryAsync(from, to, ct);
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
        var result = await analyticsService.ComparePeriodsAsync(previousFrom, previousTo, currentFrom, currentTo, ct);
        return this.ToActionResult(result);
    }
}
