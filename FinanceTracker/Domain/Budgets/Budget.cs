using Domain.Common;

namespace Domain.Budgets;

public sealed class Budget : Entity
{
    public Guid UserId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? AccountId { get; set; }
    public decimal LimitAmount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public BudgetPeriodType PeriodType { get; set; } = BudgetPeriodType.Monthly;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
