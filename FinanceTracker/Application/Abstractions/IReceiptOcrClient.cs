using Shared.Results;

namespace Application.Abstractions;

public sealed record ReceiptOcrItemResult(
    string Name,
    string CurrencyCode,
    decimal Price,
    string CategoryName);

public sealed record ReceiptOcrParseResult(
    string? Merchant,
    DateTime? PurchaseDate,
    decimal? TotalAmount,
    string? TotalCurrencyCode,
    string UploadedFileId,
    string Prompt,
    string RawResponse,
    IReadOnlyCollection<ReceiptOcrItemResult> Items);

public interface IReceiptOcrClient
{
    Task<Result<ReceiptOcrParseResult>> ParseReceiptAsync(
        Stream content,
        string fileName,
        string contentType,
        IReadOnlyCollection<string> categories,
        CancellationToken ct = default);
}
