using Application.Abstractions;
using Application.Auth;
using Domain.Common;
using Domain.Receipts;
using Shared.Results;

namespace Application.Receipts;

public sealed class ReceiptService(IFinanceDbContext dbContext, IAuthService authService)
{
    public async Task<Result<IReadOnlyCollection<Receipt>>> GetListAsync(CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<IReadOnlyCollection<Receipt>>.Failure(user.Error);

        var receipts = dbContext.Receipts.Where(r => r.UserId == user.Value.Id).OrderByDescending(r => r.UploadedAt).ToList();
        return Result<IReadOnlyCollection<Receipt>>.Success(receipts);
    }

    public async Task<Result<Receipt>> UploadAsync(string fileUrl, decimal? recognizedAmount, DateTime? recognizedDate, string? merchant, CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<Receipt>.Failure(user.Error);

        var receipt = new Receipt
        {
            UserId = user.Value.Id,
            FileUrl = fileUrl,
            UploadedAt = DateTime.UtcNow,
            OcrStatus = ReceiptOcrStatus.Completed,
            RecognizedTotalAmount = recognizedAmount,
            RecognizedDate = recognizedDate,
            RecognizedMerchant = merchant,
            RawOcrData = "{}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await dbContext.AddAsync(receipt, ct);
        await dbContext.SaveChangesAsync(ct);

        return Result<Receipt>.Success(receipt);
    }
}
