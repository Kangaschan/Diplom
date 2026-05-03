using Domain.Common;

namespace Domain.Receipts;

public sealed class Receipt : Entity
{
    public Guid UserId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string StorageContainer { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public ReceiptOcrStatus OcrStatus { get; set; } = ReceiptOcrStatus.Pending;
    public decimal? RecognizedTotalAmount { get; set; }
    public DateTime? RecognizedDate { get; set; }
    public string? RecognizedMerchant { get; set; }
    public string? RawOcrData { get; set; }
    public string? ProcessingError { get; set; }
    public Guid? CreatedTransactionId { get; set; }
}
