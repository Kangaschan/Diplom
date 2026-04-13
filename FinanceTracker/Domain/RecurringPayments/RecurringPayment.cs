using Domain.Common;

namespace Domain.RecurringPayments;

public sealed class RecurringPayment : Entity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public decimal EstimatedAmount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public string Frequency { get; set; } = "monthly";
    public DateTime? LastDetectedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
