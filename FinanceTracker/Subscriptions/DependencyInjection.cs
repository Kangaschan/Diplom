using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Subscriptions.Configuration;
using Subscriptions.Interfaces;
using Subscriptions.Services;

namespace Subscriptions;

public static class DependencyInjection
{
    public static IServiceCollection AddStripeSubscriptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        services.AddScoped<IStripeService, StripeService>();
        services.AddScoped<IStripeWebhookService, StripeWebhookService>();
        return services;
    }
}
