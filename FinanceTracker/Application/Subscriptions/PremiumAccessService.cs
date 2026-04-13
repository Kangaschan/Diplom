using Domain.Users;
using Shared.Results;

namespace Application.Subscriptions;

public sealed class PremiumAccessService : IPremiumAccessService
{
    public Result EnsurePremium(User user)
    {
        return user.HasActivePremium
            ? Result.Success()
            : Result.Failure(Abstractions.AppErrors.PremiumRequired("Premium subscription is required."));
    }
}
