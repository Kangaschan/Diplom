namespace Subscriptions.Configuration;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    public string ApiKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public string PortalReturnUrl { get; set; } = string.Empty;
    public List<StripeSubscription> Subscriptions { get; set; } = [];
}
