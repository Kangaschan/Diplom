using Application.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Constants;
using Shared.Errors;
using Shared.Results;
using Stripe;
using Stripe.Checkout;
using Subscriptions.Configuration;
using Subscriptions.Interfaces;
using Subscriptions.Models.Request;
using Subscriptions.Models.Response;
using SessionCreateOptions = Stripe.Checkout.SessionCreateOptions;

namespace Subscriptions.Services;

public sealed class StripeService : IStripeService
{
    private readonly IFinanceDbContext _dbContext;
    private readonly StripeOptions _stripeOptions;
    private readonly StripeClient _stripeClient;

    public StripeService(IFinanceDbContext dbContext, IOptions<StripeOptions> stripeOptions)
    {
        _dbContext = dbContext;
        _stripeOptions = stripeOptions.Value;
        _stripeClient = new StripeClient(_stripeOptions.ApiKey);
    }

    public Result<IReadOnlyCollection<SubscriptionInfoDto>> GetPricesInfo()
    {
        var plans = _stripeOptions.Subscriptions
            .Select(subscription => new SubscriptionInfoDto
            {
                Name = subscription.Name,
                Prices = subscription.Prices.Select(price => new PriceDto
                {
                    Id = price.Id,
                    Name = price.Name,
                    DurationDays = price.DurationDays
                })
            })
            .ToList()
            .AsReadOnly();

        return Result<IReadOnlyCollection<SubscriptionInfoDto>>.Success(plans);
    }

    public async Task<Result<string>> CreateCheckoutSessionAsync(Guid userId, CheckoutRequest request, CancellationToken cancellationToken)
    {
        var user = _dbContext.Users.FirstOrDefault(u => u.Id == userId);
        if (user is null)
        {
            return Result<string>.Failure(new AppError(ErrorCodes.NotFound, "User not found."));
        }

        if (string.IsNullOrWhiteSpace(request.PriceId))
        {
            return Result<string>.Failure(new AppError(ErrorCodes.Validation, "PriceId is required."));
        }

        var customerId = await FindOrCreateCustomerAsync(user.Id, user.Email, cancellationToken);

        var options = new SessionCreateOptions
        {
            Customer = customerId.ToString(),
            PaymentMethodTypes = new List<string> { "card" },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = request.PriceId,
                    Quantity = 1
                }
            ],
            Mode = "subscription",
            SuccessUrl = _stripeOptions.SuccessUrl,
            CancelUrl = _stripeOptions.CancelUrl,
            Metadata = new Dictionary<string, string>
            {
                ["user_id"] = user.Id.ToString()
            },
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["user_id"] = user.Id.ToString(),
                    ["price_id"] = request.PriceId
                }
            }
        };

        var sessionService = new SessionService(_stripeClient);
        var session = await sessionService.CreateAsync(options, cancellationToken: cancellationToken);
        return Result<string>.Success(session.Url);
    }

    public async Task<Result<string>> CreateCustomerPortalSessionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = _dbContext.Users.FirstOrDefault(u => u.Id == userId);
        if (user is null)
        {
            return Result<string>.Failure(new AppError(ErrorCodes.NotFound, "User not found."));
        }

        var customerId = await FindOrCreateCustomerAsync(user.Id, user.Email, cancellationToken);
        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = customerId.ToString(),
            ReturnUrl = string.IsNullOrWhiteSpace(_stripeOptions.PortalReturnUrl)
                ? _stripeOptions.SuccessUrl
                : _stripeOptions.PortalReturnUrl
        };

        var service = new Stripe.BillingPortal.SessionService(_stripeClient);
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);
        return Result<string>.Success(session.Url);
    }

    private async Task<string> FindOrCreateCustomerAsync(Guid userId, string email, CancellationToken cancellationToken)
    {
        var customerService = new CustomerService(_stripeClient);
        var existingCustomers = await customerService.ListAsync(new CustomerListOptions
        {
            Email = email,
            Limit = 100
        }, cancellationToken: cancellationToken);

        var existing = existingCustomers.Data.FirstOrDefault(customer =>
            customer.Metadata.TryGetValue("user_id", out var metadataUserId)
            && string.Equals(metadataUserId, userId.ToString(), StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            return existing.Id;
        }

        var created = await customerService.CreateAsync(new CustomerCreateOptions
        {
            Email = email,
            Metadata = new Dictionary<string, string>
            {
                ["user_id"] = userId.ToString()
            }
        }, cancellationToken: cancellationToken);

        return created.Id;
    }
}
