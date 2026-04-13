using Application.Auth;
using Identity.Models;
using Identity.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Authentication;

public static class IdentityDependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KeycloakOptions>(configuration.GetSection("Keycloak"));

        var keycloakOptions = configuration.GetSection("Keycloak").Get<KeycloakOptions>() ?? new KeycloakOptions();

        services.AddHttpClient<IIdentityAuthClient, KeycloakServiceClient>(client =>
        {
            client.BaseAddress = new Uri(keycloakOptions.BaseUrl);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloakOptions.Authority;
                options.Audience = keycloakOptions.Audience;
                options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    ValidateLifetime = true,
                    ValidIssuer = keycloakOptions.Authority,
                    ValidAudiences = keycloakOptions.ValidAudiences
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrWhiteSpace(context.Token))
                        {
                            context.HttpContext.Items["auth-debug"] = "missing_token";
                            return Task.CompletedTask;
                        }

                        if (context.Token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Token = context.Token["Bearer ".Length..].Trim();
                        }

                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        context.Response.Headers["x-auth-error"] = "invalid_token";
                        context.Response.Headers["x-auth-error-description"] = context.Exception.Message;
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        if (!context.Response.HasStarted)
                        {
                            if (context.HttpContext.Items.TryGetValue("auth-debug", out var debugReason) &&
                                debugReason is string debugReasonValue &&
                                debugReasonValue == "missing_token")
                            {
                                context.Response.Headers["x-auth-error"] = "missing_token";
                                context.Response.Headers["x-auth-error-description"] = "Authorization header was not provided.";
                                return Task.CompletedTask;
                            }

                            context.Response.Headers["x-auth-error"] = context.Error ?? "invalid_token";
                            if (!string.IsNullOrWhiteSpace(context.ErrorDescription))
                            {
                                context.Response.Headers["x-auth-error-description"] = context.ErrorDescription;
                            }
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Premium", policy => policy.RequireClaim("premium", "true"));
        });

        return services;
    }
}
