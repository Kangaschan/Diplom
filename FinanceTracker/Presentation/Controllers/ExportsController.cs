using Application.Exports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;

namespace Presentation.Controllers;

[ApiController]
[Route("api/exports")]
[Authorize]
public sealed class ExportsController(ExportService exportService) : ControllerBase
{
    [HttpGet("transactions-csv")]
    public async Task<IActionResult> ExportTransactions([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var result = await exportService.ExportTransactionsCsvAsync(from, to, ct);
        if (result.IsFailure || result.Value is null)
        {
            return this.ToActionResult(result);
        }

        return File(System.Text.Encoding.UTF8.GetBytes(result.Value), "text/csv", "transactions.csv");
    }
}
