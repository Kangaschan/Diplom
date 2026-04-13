using Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Identity.Services;

public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid UserId
    {
        get
        {
            var value = User?.FindFirstValue("local_user_id") ?? User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty;
        }
    }

    public string KeycloakUserId => User?.FindFirstValue("sub") ?? string.Empty;

    public string Username =>
        User?.FindFirstValue("preferred_username")
        ?? User?.FindFirstValue(ClaimTypes.Name)
        ?? "user";

    public string Email => User?.FindFirstValue(ClaimTypes.Email) ?? User?.FindFirstValue("email") ?? string.Empty;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
}
