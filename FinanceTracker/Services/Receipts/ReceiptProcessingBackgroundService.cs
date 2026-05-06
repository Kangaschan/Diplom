using System.Threading.Channels;
using Application.Abstractions;
using Application.Receipts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Services.Receipts;

public sealed class ReceiptProcessingBackgroundService : BackgroundService, IReceiptProcessingQueue
{
    private readonly Channel<Guid> _channel;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ReceiptProcessingBackgroundService> _logger;

    public ReceiptProcessingBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ReceiptProcessingBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public async Task QueueAsync(Guid receiptId, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(receiptId, ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var receiptId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<ReceiptProcessingService>();
                await processor.ProcessAsync(receiptId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Receipt processing background task failed for receipt {ReceiptId}.", receiptId);
            }
        }
    }
}
