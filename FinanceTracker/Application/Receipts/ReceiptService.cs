using Application.Abstractions;
using Application.Auth;
using Domain.Common;
using Domain.Receipts;
using Shared.Results;

namespace Application.Receipts;

public sealed record ReceiptItemDto(
    Guid Id,
    string Name,
    decimal Price,
    string CurrencyCode,
    string CategoryName,
    Guid? MappedCategoryId,
    string? MappedCategoryName,
    int SortOrder);

public sealed record ReceiptListItemDto(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    DateTime UploadedAt,
    ReceiptOcrStatus OcrStatus,
    decimal? RecognizedTotalAmount,
    DateTime? RecognizedDate,
    string? RecognizedMerchant,
    string? ProcessingError,
    string PreviewUrl);

public sealed record ReceiptDetailsDto(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    DateTime UploadedAt,
    ReceiptOcrStatus OcrStatus,
    decimal? RecognizedTotalAmount,
    DateTime? RecognizedDate,
    string? RecognizedMerchant,
    string? ProcessingError,
    string PreviewUrl,
    IReadOnlyCollection<ReceiptItemDto> Items);

public sealed record ReceiptFileDownloadDto(
    Stream Content,
    string ContentType,
    string FileName);

public sealed class ReceiptService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png"
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private readonly IFinanceDbContext _dbContext;
    private readonly IAuthService _authService;
    private readonly IReceiptFileStorage _receiptFileStorage;

    public ReceiptService(
        IFinanceDbContext dbContext,
        IAuthService authService,
        IReceiptFileStorage receiptFileStorage)
    {
        _dbContext = dbContext;
        _authService = authService;
        _receiptFileStorage = receiptFileStorage;
    }

    public async Task<Result<IReadOnlyCollection<ReceiptListItemDto>>> GetListAsync(CancellationToken ct = default)
    {
        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result<IReadOnlyCollection<ReceiptListItemDto>>.Failure(user.Error);
        }

        var receipts = _dbContext.Receipts
            .Where(receipt => receipt.UserId == user.Value.Id)
            .OrderByDescending(receipt => receipt.UploadedAt)
            .ToList()
            .Select(MapListItem)
            .ToList();

        return Result<IReadOnlyCollection<ReceiptListItemDto>>.Success(receipts);
    }

    public async Task<Result<ReceiptDetailsDto>> GetByIdAsync(Guid receiptId, CancellationToken ct = default)
    {
        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result<ReceiptDetailsDto>.Failure(user.Error);
        }

        var receipt = _dbContext.Receipts.FirstOrDefault(entity => entity.Id == receiptId && entity.UserId == user.Value.Id);
        if (receipt is null)
        {
            return Result<ReceiptDetailsDto>.Failure(AppErrors.NotFound("Receipt not found."));
        }

        var receiptItems = _dbContext.ReceiptItems
            .Where(item => item.ReceiptId == receipt.Id)
            .OrderBy(item => item.SortOrder)
            .ToList();

        var categoryMap = _dbContext.Categories
            .Where(category => category.IsSystem || category.UserId == user.Value.Id)
            .ToDictionary(category => category.Id, category => category.Name);

        var details = new ReceiptDetailsDto(
            receipt.Id,
            receipt.OriginalFileName,
            receipt.ContentType,
            receipt.FileSizeBytes,
            receipt.UploadedAt,
            receipt.OcrStatus,
            receipt.RecognizedTotalAmount,
            receipt.RecognizedDate,
            receipt.RecognizedMerchant,
            receipt.ProcessingError,
            BuildPreviewUrl(receipt.Id),
            receiptItems.Select(item => new ReceiptItemDto(
                item.Id,
                item.Name,
                item.Price,
                item.CurrencyCode,
                item.CategoryName,
                item.MappedCategoryId,
                item.MappedCategoryId.HasValue && categoryMap.TryGetValue(item.MappedCategoryId.Value, out var mappedCategoryName)
                    ? mappedCategoryName
                    : null,
                item.SortOrder)).ToList());

        return Result<ReceiptDetailsDto>.Success(details);
    }

    public async Task<Result<ReceiptDetailsDto>> UploadAsync(
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        Stream fileStream,
        CancellationToken ct = default)
    {
        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result<ReceiptDetailsDto>.Failure(user.Error);
        }

        var validationResult = ValidateUpload(originalFileName, fileSizeBytes);
        if (validationResult.IsFailure)
        {
            return Result<ReceiptDetailsDto>.Failure(validationResult.Error);
        }

        var receiptId = Guid.NewGuid();
        var safeFileName = Path.GetFileName(originalFileName);
        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        var blobName = $"{user.Value.Id}/{receiptId}/{Guid.NewGuid():N}{extension}";

        var storedFile = await _receiptFileStorage.UploadAsync(fileStream, contentType, blobName, ct);

        var receipt = new Receipt
        {
            Id = receiptId,
            UserId = user.Value.Id,
            FileUrl = storedFile.BlobName,
            StorageContainer = storedFile.ContainerName,
            OriginalFileName = safeFileName,
            ContentType = storedFile.ContentType,
            FileSizeBytes = storedFile.ContentLength,
            UploadedAt = DateTime.UtcNow,
            OcrStatus = ReceiptOcrStatus.Pending,
            RawOcrData = "{}",
            ProcessingError = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _dbContext.AddAsync(receipt, ct);
        await _dbContext.SaveChangesAsync(ct);

        var details = new ReceiptDetailsDto(
            receipt.Id,
            receipt.OriginalFileName,
            receipt.ContentType,
            receipt.FileSizeBytes,
            receipt.UploadedAt,
            receipt.OcrStatus,
            receipt.RecognizedTotalAmount,
            receipt.RecognizedDate,
            receipt.RecognizedMerchant,
            receipt.ProcessingError,
            BuildPreviewUrl(receipt.Id),
            []);

        return Result<ReceiptDetailsDto>.Success(details);
    }

    public async Task<Result<ReceiptFileDownloadDto>> OpenFileAsync(Guid receiptId, CancellationToken ct = default)
    {
        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result<ReceiptFileDownloadDto>.Failure(user.Error);
        }

        var receipt = _dbContext.Receipts.FirstOrDefault(entity => entity.Id == receiptId && entity.UserId == user.Value.Id);
        if (receipt is null)
        {
            return Result<ReceiptFileDownloadDto>.Failure(AppErrors.NotFound("Receipt not found."));
        }

        var file = await _receiptFileStorage.OpenReadAsync(receipt.StorageContainer, receipt.FileUrl, ct);
        if (file is null)
        {
            return Result<ReceiptFileDownloadDto>.Failure(AppErrors.NotFound("Receipt file not found."));
        }

        return Result<ReceiptFileDownloadDto>.Success(new ReceiptFileDownloadDto(file.Content, file.ContentType, receipt.OriginalFileName));
    }

    public async Task<Result<ReceiptItemDto>> UpdateItemCategoryAsync(Guid receiptItemId, Guid? mappedCategoryId, CancellationToken ct = default)
    {
        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result<ReceiptItemDto>.Failure(user.Error);
        }

        var receiptItem = _dbContext.ReceiptItems.FirstOrDefault(item => item.Id == receiptItemId);
        if (receiptItem is null)
        {
            return Result<ReceiptItemDto>.Failure(AppErrors.NotFound("Receipt item not found."));
        }

        var receipt = _dbContext.Receipts.FirstOrDefault(entity => entity.Id == receiptItem.ReceiptId && entity.UserId == user.Value.Id);
        if (receipt is null)
        {
            return Result<ReceiptItemDto>.Failure(AppErrors.NotFound("Receipt not found."));
        }

        Guid? resolvedCategoryId = mappedCategoryId;
        if (resolvedCategoryId.HasValue)
        {
            var categoryExists = _dbContext.Categories.Any(category =>
                category.Id == resolvedCategoryId.Value
                && category.IsActive
                && (category.UserId == user.Value.Id || category.IsSystem));

            if (!categoryExists)
            {
                return Result<ReceiptItemDto>.Failure(AppErrors.Validation("Category not found."));
            }
        }
        else
        {
            resolvedCategoryId = _dbContext.Categories
                .Where(category =>
                    category.UserId == user.Value.Id
                    && category.Type == Domain.Common.CategoryType.Expense
                    && category.Name == "Others")
                .Select(category => (Guid?)category.Id)
                .FirstOrDefault();
        }

        receiptItem.MappedCategoryId = resolvedCategoryId;
        receiptItem.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        string? mappedCategoryName = null;
        if (receiptItem.MappedCategoryId.HasValue)
        {
            mappedCategoryName = _dbContext.Categories
                .Where(category => category.Id == receiptItem.MappedCategoryId.Value)
                .Select(category => category.Name)
                .FirstOrDefault();
        }

        return Result<ReceiptItemDto>.Success(new ReceiptItemDto(
            receiptItem.Id,
            receiptItem.Name,
            receiptItem.Price,
            receiptItem.CurrencyCode,
            receiptItem.CategoryName,
            receiptItem.MappedCategoryId,
            mappedCategoryName,
            receiptItem.SortOrder));
    }

    private static Result ValidateUpload(string originalFileName, long fileSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return Result.Failure(AppErrors.Validation("File name is required."));
        }

        if (fileSizeBytes <= 0)
        {
            return Result.Failure(AppErrors.Validation("Uploaded file is empty."));
        }

        if (fileSizeBytes > MaxFileSizeBytes)
        {
            return Result.Failure(AppErrors.Validation("File size must not exceed 10 MB."));
        }

        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return Result.Failure(AppErrors.Validation("Only JPG, JPEG and PNG files are supported."));
        }

        return Result.Success();
    }

    private static ReceiptListItemDto MapListItem(Receipt receipt)
    {
        return new ReceiptListItemDto(
            receipt.Id,
            receipt.OriginalFileName,
            receipt.ContentType,
            receipt.FileSizeBytes,
            receipt.UploadedAt,
            receipt.OcrStatus,
            receipt.RecognizedTotalAmount,
            receipt.RecognizedDate,
            receipt.RecognizedMerchant,
            receipt.ProcessingError,
            BuildPreviewUrl(receipt.Id));
    }

    private static string BuildPreviewUrl(Guid receiptId)
    {
        return $"/receipts/{receiptId}/file";
    }
}
