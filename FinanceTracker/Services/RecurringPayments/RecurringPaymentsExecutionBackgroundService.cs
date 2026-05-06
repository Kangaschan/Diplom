using Application.RecurringPayments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Services.RecurringPayments;

public sealed class RecurringPaymentsExecutionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<RecurringPaymentsExecutionBackgroundService> _logger;
    private readonly RecurringPaymentsExecutionOptions _options;

    public RecurringPaymentsExecutionBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<RecurringPaymentsExecutionOptions> options,
        ILogger<RecurringPaymentsExecutionBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollingInterval = TimeSpan.FromSeconds(Math.Max(15, _options.PollingIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var recurringPaymentService = scope.ServiceProvider.GetRequiredService<RecurringPaymentService>();
                await recurringPaymentService.ProcessDuePaymentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Recurring payments execution cycle failed.");
            }

            try
            {
                await Task.Delay(pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
