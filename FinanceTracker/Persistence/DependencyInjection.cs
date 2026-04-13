using Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Db;

namespace Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var useInMemory = configuration.GetValue<bool>("Persistence:UseInMemory");
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? "Host=localhost;Port=5432;Database=financetracker;Username=postgres;Password=postgres";

        services.AddDbContext<FinanceDbContext>(options =>
        {
            if (useInMemory)
            {
                options.UseInMemoryDatabase("financetracker-mvp");
            }
            else
            {
                options.UseNpgsql(connectionString);
            }
        });
        services.AddScoped<IFinanceDbContext>(provider => provider.GetRequiredService<FinanceDbContext>());

        return services;
    }
}
