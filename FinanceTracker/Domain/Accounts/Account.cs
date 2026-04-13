using Domain.Common;

namespace Domain.Accounts;

public sealed class Account : Entity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "USD";
    public decimal CurrentBalance { get; set; }
    public decimal? FinancialGoalAmount { get; set; }
    public DateTime? FinancialGoalDeadline { get; set; }
    public bool IsArchived { get; set; }
}
