namespace Subscriptions.Configuration;

public sealed class StripeSubscription
{
    public string Name { get; set; } = string.Empty;
    public List<StripePriceInfo> Prices { get; set; } = [];
}
