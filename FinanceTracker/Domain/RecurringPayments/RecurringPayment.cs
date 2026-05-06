using Domain.Common;

namespace Domain.RecurringPayments;

public sealed class RecurringPayment : Entity
{
    public Guid UserId { get; set; }
    public Guid? AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public TransactionType Type { get; set; } = TransactionType.Expense;
    public decimal EstimatedAmount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public string Frequency { get; set; } = "monthly";
    public DateTime? StartDate { get; set; }
    public DateTime? NextExecutionAt { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? LastDetectedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
