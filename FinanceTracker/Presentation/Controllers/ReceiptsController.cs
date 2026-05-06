using Application.Receipts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;
using Shared.Constants;

namespace Presentation.Controllers;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1050", Justification = "Local API contract")]
public sealed record UpdateReceiptItemCategoryRequest(Guid? MappedCategoryId);

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1050", Justification = "Local API contract")]
public sealed record UpdateReceiptItemRequest(
    string Name,
    decimal Price,
    string CurrencyCode,
    Guid? MappedCategoryId);

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1050", Justification = "Local API contract")]
public sealed record CreateReceiptItemRequest(
    string Name,
    decimal Price,
    string CurrencyCode,
    Guid? MappedCategoryId);

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1050", Justification = "Local API contract")]
public sealed record ApplyReceiptRequest(Guid AccountId, DateTime? TransactionDate);

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1050", Justification = "Local API contract")]
public sealed class UploadReceiptRequest
{
    public IFormFile? File { get; set; }
}

[ApiController]
[Route("api/receipts")]
[Authorize]
public sealed class ReceiptsController : ControllerBase
{
    private readonly ReceiptService _receiptService;

    public ReceiptsController(ReceiptService receiptService)
    {
        _receiptService = receiptService;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _receiptService.GetListAsync(ct);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _receiptService.GetByIdAsync(id, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] UploadReceiptRequest request, CancellationToken ct)
    {
        var file = request.File;
        if (file is null)
        {
            return this.ToActionResult(Shared.Results.Result.Failure(new Shared.Errors.AppError(ErrorCodes.Validation,"file is missing")));
        }

        await using var stream = file.OpenReadStream();
        var result = await _receiptService.UploadAsync(
            file.FileName,
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            file.Length,
            stream,
            ct);

        return this.ToActionResult(result);
    }

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> FileContent(Guid id, CancellationToken ct)
    {
        var result = await _receiptService.OpenFileAsync(id, ct);
        if (result.IsFailure || result.Value is null)
        {
            return this.ToActionResult(result);
        }

        return File(result.Value.Content, result.Value.ContentType, enableRangeProcessing: true);
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var result = await _receiptService.RetryAsync(id, ct);
        return this.ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _receiptService.DeleteAsync(id, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("{id:guid}/apply")]
    public async Task<IActionResult> Apply(Guid id, [FromBody] ApplyReceiptRequest request, CancellationToken ct)
    {
        var result = await _receiptService.ApplyToAccountAsync(id, request.AccountId, request.TransactionDate, ct);
        return this.ToActionResult(result);
    }

    [HttpPut("items/{itemId:guid}/category")]
    public async Task<IActionResult> UpdateItemCategory(Guid itemId, [FromBody] UpdateReceiptItemCategoryRequest request, CancellationToken ct)
    {
        var result = await _receiptService.UpdateItemCategoryAsync(itemId, request.MappedCategoryId, ct);
        return this.ToActionResult(result);
    }

    [HttpPut("items/{itemId:guid}")]
    public async Task<IActionResult> UpdateItem(Guid itemId, [FromBody] UpdateReceiptItemRequest request, CancellationToken ct)
    {
        var result = await _receiptService.UpdateItemAsync(
            itemId,
            request.Name,
            request.Price,
            request.CurrencyCode,
            request.MappedCategoryId,
            ct);
        return this.ToActionResult(result);
    }

    [HttpPost("{id:guid}/items")]
    public async Task<IActionResult> CreateItem(Guid id, [FromBody] CreateReceiptItemRequest request, CancellationToken ct)
    {
        var result = await _receiptService.CreateItemAsync(
            id,
            request.Name,
            request.Price,
            request.CurrencyCode,
            request.MappedCategoryId,
            ct);
        return this.ToActionResult(result);
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid itemId, CancellationToken ct)
    {
        var result = await _receiptService.DeleteItemAsync(itemId, ct);
        return this.ToActionResult(result);
    }
}
