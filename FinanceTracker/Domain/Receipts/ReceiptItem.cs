using Domain.Common;

namespace Domain.Receipts;

public sealed class ReceiptItem : Entity
{
    public Guid ReceiptId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public Guid? MappedCategoryId { get; set; }
    public int SortOrder { get; set; }
}
