using Domain.Common;

namespace Domain.Subscriptions;

public sealed class Subscription : Entity
{
    public Guid UserId { get; set; }
    public SubscriptionType Type { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
