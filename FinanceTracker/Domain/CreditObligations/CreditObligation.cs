using Domain.Common;

namespace Domain.CreditObligations;

public sealed class CreditObligation : Entity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal RemainingAmount { get; set; }
    public decimal MonthlyPayment { get; set; }
    public decimal InterestRate { get; set; }
    public DateTime? EndDate { get; set; }
}
