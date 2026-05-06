namespace Application.Abstractions;

public interface IReceiptProcessingQueue
{
    Task QueueAsync(Guid receiptId, CancellationToken ct = default);
}
