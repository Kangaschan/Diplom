using Application.Abstractions;
using Application.Auth;
using Domain.Common;
using Domain.Subscriptions;
using Shared.Results;

namespace Application.Subscriptions;

public sealed class SubscriptionService(IFinanceDbContext dbContext, IAuthService authService)
{
    public async Task<Result<Subscription?>> GetCurrentAsync(CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<Subscription?>.Failure(user.Error);

        var current = dbContext.Subscriptions
            .Where(s => s.UserId == user.Value.Id && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.EndDate)
            .FirstOrDefault();

        return Result<Subscription?>.Success(current);
    }

    public async Task<Result<IReadOnlyCollection<Subscription>>> GetHistoryAsync(CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<IReadOnlyCollection<Subscription>>.Failure(user.Error);

        var history = dbContext.Subscriptions
            .Where(s => s.UserId == user.Value.Id)
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        return Result<IReadOnlyCollection<Subscription>>.Success(history);
    }

    public async Task<Result<Subscription>> ActivatePremiumAsync(int durationDays, CancellationToken ct = default)
    {
        var userResult = await authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null) return Result<Subscription>.Failure(userResult.Error);

        var user = userResult.Value;

        var now = DateTime.UtcNow;
        var start = user.SubscriptionExpiresAt is not null && user.SubscriptionExpiresAt > now
            ? user.SubscriptionExpiresAt.Value
            : now;

        var subscription = new Subscription
        {
            UserId = user.Id,
            Type = SubscriptionType.Premium,
            Status = SubscriptionStatus.Active,
            StartDate = start,
            EndDate = start.AddDays(Math.Max(durationDays, 1)),
            CreatedAt = now,
            UpdatedAt = now
        };

        await dbContext.AddAsync(subscription, ct);

        user.CurrentSubscriptionType = SubscriptionType.Premium;
        user.HasActivePremium = true;
        user.SubscriptionExpiresAt = subscription.EndDate;
        user.UpdatedAt = now;

        await dbContext.SaveChangesAsync(ct);
        return Result<Subscription>.Success(subscription);
    }

    public async Task<Result> CancelPremiumAsync(CancellationToken ct = default)
    {
        var userResult = await authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null) return Result.Failure(userResult.Error);

        var user = userResult.Value;
        var active = dbContext.Subscriptions
            .Where(s => s.UserId == user.Id && s.Status == SubscriptionStatus.Active)
            .ToList();

        foreach (var subscription in active)
        {
            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.UpdatedAt = DateTime.UtcNow;
        }

        user.HasActivePremium = false;
        user.CurrentSubscriptionType = SubscriptionType.Free;
        user.SubscriptionExpiresAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task SyncSubscriptionStateAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expired = dbContext.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active && s.EndDate < now)
            .ToList();

        foreach (var subscription in expired)
        {
            subscription.Status = SubscriptionStatus.Expired;
            subscription.UpdatedAt = now;

            var user = dbContext.Users.FirstOrDefault(u => u.Id == subscription.UserId);
            if (user is null) continue;

            var hasAnotherActive = dbContext.Subscriptions.Any(s =>
                s.UserId == user.Id && s.Status == SubscriptionStatus.Active && s.EndDate >= now && s.Id != subscription.Id);

            if (!hasAnotherActive)
            {
                user.HasActivePremium = false;
                user.CurrentSubscriptionType = SubscriptionType.Free;
                user.SubscriptionExpiresAt = null;
                user.UpdatedAt = now;
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
