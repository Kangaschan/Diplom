using Application.Abstractions;
using Domain.Common;
using Microsoft.Extensions.Options;
using Shared.Constants;
using Shared.Errors;
using Shared.Results;
using Stripe;
using Subscriptions.Configuration;
using Subscriptions.Interfaces;
using Subscriptions.Models.Response;
using Subscription = Domain.Subscriptions.Subscription;

namespace Subscriptions.Services;

public sealed class StripeWebhookService : IStripeWebhookService
{
    private readonly IFinanceDbContext _dbContext;
    private readonly StripeOptions _stripeOptions;
    private readonly StripeClient _stripeClient;

    public StripeWebhookService(IFinanceDbContext dbContext, IOptions<StripeOptions> stripeOptions)
    {
        _dbContext = dbContext;
        _stripeOptions = stripeOptions.Value;
        _stripeClient = new StripeClient(_stripeOptions.ApiKey);
    }

    public async Task<Result> HandleWebhookAsync(StrpeWebHookMessage message, CancellationToken cancellationToken)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                message.Json,
                message.Headers,
                _stripeOptions.WebhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (Exception ex)
        {
            return Result.Failure(new AppError(ErrorCodes.Validation, $"Invalid webhook signature: {ex.Message}"));
        }

        switch (stripeEvent.Type)
        {
            case "customer.subscription.created":
            case "customer.subscription.updated":
            case "customer.subscription.deleted":
                {
                    var stripeSubscription = stripeEvent.Data.Object as Stripe.Subscription;
                    if (stripeSubscription is null)
                    {
                        return Result.Failure(new AppError(ErrorCodes.Validation, "Webhook payload does not contain subscription object."));
                    }

                    var resolveResult = await ResolveUserIdAsync(stripeSubscription, cancellationToken);
                    if (resolveResult.IsFailure || resolveResult.Value == Guid.Empty)
                    {
                        return Result.Failure(resolveResult.Error);
                    }

                    await UpsertSubscriptionStateAsync(resolveResult.Value, stripeSubscription, cancellationToken);
                    return Result.Success();
                }
            default:
                return Result.Success();
        }
    }

    private async Task<Result<Guid>> ResolveUserIdAsync(Stripe.Subscription stripeSubscription, CancellationToken cancellationToken)
    {
        if (stripeSubscription.Metadata.TryGetValue("user_id", out var userIdValue)
            && Guid.TryParse(userIdValue, out var userId))
        {
            return Result<Guid>.Success(userId);
        }

        var customerId = stripeSubscription.CustomerId;
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return Result<Guid>.Failure(new AppError(ErrorCodes.Validation, "CustomerId is missing in subscription webhook."));
        }

        var customerService = new CustomerService(_stripeClient);
        var customer = await customerService.GetAsync(customerId, cancellationToken: cancellationToken);

        if (customer.Metadata.TryGetValue("user_id", out var metadataUserId)
            && Guid.TryParse(metadataUserId, out userId))
        {
            return Result<Guid>.Success(userId);
        }

        return Result<Guid>.Failure(new AppError(ErrorCodes.NotFound, "Failed to map Stripe customer to local user."));
    }

    private async Task UpsertSubscriptionStateAsync(Guid userId, Stripe.Subscription stripeSubscription, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var start = stripeSubscription.Items.Data.FirstOrDefault()?.CurrentPeriodStart ?? now;
        var end = stripeSubscription.Items.Data.FirstOrDefault()?.CurrentPeriodEnd ?? now;
        var localStatus = MapSubscriptionStatus(stripeSubscription.Status, end, stripeSubscription.CancelAtPeriodEnd);
        var hasActivePremium = localStatus == SubscriptionStatus.Active;

        var existingActive = _dbContext.Subscriptions
            .Where(subscription => subscription.UserId == userId && subscription.Status == SubscriptionStatus.Active)
            .OrderByDescending(subscription => subscription.EndDate)
            .FirstOrDefault();

        if (existingActive is null)
        {
            await _dbContext.AddAsync(new Subscription
            {
                UserId = userId,
                Type = hasActivePremium ? SubscriptionType.Premium : SubscriptionType.Free,
                Status = localStatus,
                StartDate = start,
                EndDate = end,
                CreatedAt = now,
                UpdatedAt = now
            }, cancellationToken);
        }
        else
        {
            existingActive.Status = localStatus;
            existingActive.Type = hasActivePremium ? SubscriptionType.Premium : SubscriptionType.Free;
            existingActive.StartDate = start;
            existingActive.EndDate = end;
            existingActive.UpdatedAt = now;
        }

        var user = _dbContext.Users.FirstOrDefault(entity => entity.Id == userId);
        if (user is not null)
        {
            user.HasActivePremium = hasActivePremium;
            user.CurrentSubscriptionType = hasActivePremium ? SubscriptionType.Premium : SubscriptionType.Free;
            user.SubscriptionExpiresAt = hasActivePremium ? end : null;
            user.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static SubscriptionStatus MapSubscriptionStatus(string? stripeStatus, DateTime periodEnd, bool cancelAtPeriodEnd)
    {
        if (string.Equals(stripeStatus, "canceled", StringComparison.OrdinalIgnoreCase))
        {
            return SubscriptionStatus.Cancelled;
        }

        if (cancelAtPeriodEnd && periodEnd <= DateTime.UtcNow)
        {
            return SubscriptionStatus.Cancelled;
        }

        if (periodEnd < DateTime.UtcNow
            || string.Equals(stripeStatus, "incomplete_expired", StringComparison.OrdinalIgnoreCase)
            || string.Equals(stripeStatus, "unpaid", StringComparison.OrdinalIgnoreCase))
        {
            return SubscriptionStatus.Expired;
        }

        return SubscriptionStatus.Active;
    }
}
