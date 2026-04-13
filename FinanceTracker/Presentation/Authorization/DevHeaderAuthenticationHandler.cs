using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Presentation.Authorization;

public sealed class DevHeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers["x-user-id"].FirstOrDefault() ?? Guid.NewGuid().ToString();
        var keycloakId = Request.Headers["x-keycloak-user-id"].FirstOrDefault() ?? $"kc-{userId}";
        var username = Request.Headers["x-username"].FirstOrDefault() ?? "demo-user";
        var email = Request.Headers["x-email"].FirstOrDefault() ?? "demo@local.test";
        var premium = Request.Headers["x-premium"].FirstOrDefault() ?? "false";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", keycloakId),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Email, email),
            new("preferred_username", username),
            new("premium", premium)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
