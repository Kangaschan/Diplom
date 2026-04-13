using Application.Receipts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;

namespace Presentation.Controllers;

public sealed record UploadReceiptRequest(string FileUrl, decimal? RecognizedAmount, DateTime? RecognizedDate, string? Merchant);

[ApiController]
[Route("api/receipts")]
[Authorize]
public sealed class ReceiptsController(ReceiptService receiptService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await receiptService.GetListAsync(ct);
        return this.ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Upload([FromBody] UploadReceiptRequest request, CancellationToken ct)
    {
        var result = await receiptService.UploadAsync(request.FileUrl, request.RecognizedAmount, request.RecognizedDate, request.Merchant, ct);
        return this.ToActionResult(result);
    }
}
