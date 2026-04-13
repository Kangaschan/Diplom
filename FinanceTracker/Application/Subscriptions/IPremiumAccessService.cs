using Domain.Users;
using Shared.Results;

namespace Application.Subscriptions;

public interface IPremiumAccessService
{
    Result EnsurePremium(User user);
}
