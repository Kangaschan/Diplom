using Application.Accounts;
using Application.Analytics;
using Application.Auth;
using Application.Budgets;
using Application.Categories;
using Application.CreditObligations;
using Application.Exports;
using Application.Notifications;
using Application.Receipts;
using Application.RecurringPayments;
using Application.Subscriptions;
using Application.Transactions;
using Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IPremiumAccessService, PremiumAccessService>();

        services.AddScoped<SubscriptionService>();
        services.AddScoped<AccountService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<TransactionService>();
        services.AddScoped<BudgetService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<ReceiptService>();
        services.AddScoped<ExportService>();
        services.AddScoped<RecurringPaymentService>();
        services.AddScoped<CreditObligationService>();

        return services;
    }
}
