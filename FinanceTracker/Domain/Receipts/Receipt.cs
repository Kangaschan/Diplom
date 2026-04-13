using Domain.Common;

namespace Domain.Receipts;

public sealed class Receipt : Entity
{
    public Guid UserId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public ReceiptOcrStatus OcrStatus { get; set; } = ReceiptOcrStatus.Pending;
    public decimal? RecognizedTotalAmount { get; set; }
    public DateTime? RecognizedDate { get; set; }
    public string? RecognizedMerchant { get; set; }
    public string? RawOcrData { get; set; }
    public Guid? CreatedTransactionId { get; set; }
}
