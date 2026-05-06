using Application.Analytics;
using Application.Exports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;

namespace Presentation.Controllers;

[ApiController]
[Route("api/exports")]
[Authorize]
public sealed class ExportsController : ControllerBase
{
    private readonly ExportService _exportService;

    public ExportsController(ExportService exportService)
    {
        _exportService = exportService;
    }

    [HttpGet("transactions-csv")]
    public async Task<IActionResult> ExportTransactions([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var result = await _exportService.ExportTransactionsCsvAsync(from, to, ct);
        if (result.IsFailure || result.Value is null)
        {
            return this.ToActionResult(result);
        }

        return File(System.Text.Encoding.UTF8.GetBytes(result.Value), "text/csv", "transactions.csv");
    }

    [HttpGet("analytics-pdf")]
    public async Task<IActionResult> ExportAnalyticsPdf(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] AnalyticsGrouping grouping,
        CancellationToken ct)
    {
        var result = await _exportService.ExportAnalyticsPdfAsync(from, to, grouping, ct);
        if (result.IsFailure || result.Value is null)
        {
            return this.ToActionResult(result);
        }

        return File(result.Value, "application/pdf", $"analytics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf");
    }
}
