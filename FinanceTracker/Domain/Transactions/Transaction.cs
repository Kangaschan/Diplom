using Domain.Common;

namespace Domain.Transactions;

public sealed class Transaction : Entity
{
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? RecurringPaymentId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public DateTime TransactionDate { get; set; }
    public string? Description { get; set; }
    public TransactionSource Source { get; set; } = TransactionSource.Manual;
    public TransactionStatus Status { get; set; } = TransactionStatus.Posted;
}
