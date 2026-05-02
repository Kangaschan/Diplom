using Shared.Results;
using Subscriptions.Models.Response;

namespace Subscriptions.Interfaces;

public interface IStripeWebhookService
{
    Task<Result> HandleWebhookAsync(StrpeWebHookMessage message, CancellationToken cancellationToken);
}
