using Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Identity.Services;

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? GetPrincipal()
    {
        return _httpContextAccessor.HttpContext?.User;
    }

    private string? GetClaimValue(string claimType)
    {
        var principal = GetPrincipal();
        return principal?.FindFirstValue(claimType);
    }

    public Guid UserId
    {
        get
        {
            var value = GetClaimValue("local_user_id") ?? GetClaimValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty;
        }
    }

    public string KeycloakUserId
    {
        get
        {
            return GetClaimValue("sub") ?? string.Empty;
        }
    }

    public string Username
    {
        get
        {
            return GetClaimValue("preferred_username")
                   ?? GetClaimValue(ClaimTypes.Name)
                   ?? "user";
        }
    }

    public string Email
    {
        get
        {
            return GetClaimValue(ClaimTypes.Email)
                   ?? GetClaimValue("email")
                   ?? string.Empty;
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            var principal = GetPrincipal();
            return principal?.Identity?.IsAuthenticated == true;
        }
    }
}
