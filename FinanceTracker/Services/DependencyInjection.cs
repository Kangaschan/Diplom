using Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.ExchangeRates;

namespace Services;

public static class DependencyInjection
{
    public static IServiceCollection AddExternalServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ExchangeRateApiOptions>(configuration.GetSection(ExchangeRateApiOptions.SectionName));

        services.AddHttpClient<CurrencyRateCacheService>((serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<ExchangeRateApiOptions>>()
                .Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddSingleton<CurrencyRateCacheService>();
        services.AddSingleton<ICurrencyRateProvider>(provider => provider.GetRequiredService<CurrencyRateCacheService>());
        services.AddSingleton<IDashboardCurrencyProvider, DashboardCurrencyProvider>();
        services.AddHostedService(provider => provider.GetRequiredService<CurrencyRateCacheService>());

        return services;
    }
}
