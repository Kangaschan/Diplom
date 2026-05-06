using Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Services.ExchangeRates;
using Services.RecurringPayments;
using Services.Receipts;

namespace Services;

public static class DependencyInjection
{
    public static IServiceCollection AddExternalServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ExchangeRateApiOptions>(configuration.GetSection(ExchangeRateApiOptions.SectionName));
        services.Configure<GigaChatReceiptOcrOptions>(configuration.GetSection(GigaChatReceiptOcrOptions.SectionName));
        services.Configure<RecurringPaymentsExecutionOptions>(configuration.GetSection(RecurringPaymentsExecutionOptions.SectionName));

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
        services.AddHttpClient("GigaChatReceiptOcr", client =>
            {
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
            {
                var options = serviceProvider
                    .GetRequiredService<IOptions<GigaChatReceiptOcrOptions>>()
                    .Value;

                var handler = new HttpClientHandler();
                if (options.IgnoreSslErrors)
                {
                    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                }

                return handler;
            });
        services.AddSingleton<IReceiptOcrClient, GigaChatReceiptOcrClient>();
        services.AddSingleton<ReceiptProcessingBackgroundService>();
        services.AddSingleton<IReceiptProcessingQueue>(provider => provider.GetRequiredService<ReceiptProcessingBackgroundService>());
        services.AddHostedService(provider => provider.GetRequiredService<ReceiptProcessingBackgroundService>());
        services.AddHostedService<RecurringPaymentsExecutionBackgroundService>();

        return services;
    }
}
