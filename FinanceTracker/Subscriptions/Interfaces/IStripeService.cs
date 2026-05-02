using Shared.Results;
using Subscriptions.Models.Request;
using Subscriptions.Models.Response;

namespace Subscriptions.Interfaces;

public interface IStripeService
{
    Result<IReadOnlyCollection<SubscriptionInfoDto>> GetPricesInfo();
    Task<Result<string>> CreateCheckoutSessionAsync(Guid userId, CheckoutRequest request, CancellationToken cancellationToken);
    Task<Result<string>> CreateCustomerPortalSessionAsync(Guid userId, CancellationToken cancellationToken);
}
