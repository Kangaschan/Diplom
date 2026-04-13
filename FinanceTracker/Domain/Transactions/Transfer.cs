namespace Domain.Transactions;

public sealed class Transfer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public DateTime TransferDate { get; set; }
    public string? Description { get; set; }
    public Guid OutgoingTransactionId { get; set; }
    public Guid IncomingTransactionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
