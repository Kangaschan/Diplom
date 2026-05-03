using Application.Abstractions;
using Azure.Storage.Blobs;
using Infrastructure.Storage.Configuration;
using Infrastructure.Storage.Receipts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AzuriteStorageOptions>(configuration.GetSection(AzuriteStorageOptions.SectionName));

        services.AddSingleton(provider =>
        {
            var options = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<AzuriteStorageOptions>>()
                .Value;

            return new BlobServiceClient(options.ConnectionString);
        });

        services.AddSingleton<IReceiptFileStorage, AzuriteReceiptFileStorage>();
        return services;
    }
}
