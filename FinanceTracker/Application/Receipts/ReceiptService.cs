using Application.Abstractions;
using Application.Auth;
using Application.Budgets;
using Domain.Categories;
using Domain.Common;
using Domain.Receipts;
using Domain.Transactions;
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
    string? RecognizedCurrencyCode,
    DateTime? RecognizedDate,
    string? RecognizedMerchant,
    string? ProcessingError,
    string PreviewUrl,
    bool HasCreatedTransactions);

public sealed record ReceiptDetailsDto(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    DateTime UploadedAt,
    ReceiptOcrStatus OcrStatus,
    decimal? RecognizedTotalAmount,
    string? RecognizedCurrencyCode,
    DateTime? RecognizedDate,
    string? RecognizedMerchant,
    string? ProcessingError,
    string PreviewUrl,
    bool HasCreatedTransactions,
    IReadOnlyCollection<ReceiptItemDto> Items);

public sealed record ReceiptFileDownloadDto(
    Stream Content,
    string ContentType,
    string FileName);

public sealed record ReceiptApplyResult(
    Guid ReceiptId,
    Guid AccountId,
    int CreatedTransactionsCount);

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
    private readonly IReceiptProcessingQueue _receiptProcessingQueue;
    private readonly ICurrencyRateProvider _currencyRateProvider;
    private readonly BudgetService _budgetService;

    public ReceiptService(
        IFinanceDbContext dbContext,
        IAuthService authService,
        IReceiptFileStorage receiptFileStorage,
        IReceiptProcessingQueue receiptProcessingQueue,
        ICurrencyRateProvider currencyRateProvider,
        BudgetService budgetService)
    {
        _dbContext = dbContext;
        _authService = authService;
        _receiptFileStorage = receiptFileStorage;
        _receiptProcessingQueue = receiptProcessingQueue;
        _currencyRateProvider = currencyRateProvider;
        _budgetService = budgetService;
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
            .ToList();

        var receiptCurrencyLookup = _dbContext.ReceiptItems
            .Where(item => receipts.Select(receipt => receipt.Id).Contains(item.ReceiptId))
            .OrderBy(item => item.SortOrder)
            .ToList()
            .GroupBy(item => item.ReceiptId)
            .ToDictionary(group => group.Key, group => ResolveReceiptCurrencyCode(group.ToList()));

        var result = receipts
            .Select(receipt => MapListItem(
                receipt,
                receiptCurrencyLookup.TryGetValue(receipt.Id, out var recognizedCurrencyCode)
                    ? recognizedCurrencyCode
                    : null))
            .ToList();

        return Result<IReadOnlyCollection<ReceiptListItemDto>>.Success(result);
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
            ResolveReceiptCurrencyCode(receiptItems),
            receipt.RecognizedDate,
            receipt.RecognizedMerchant,
            receipt.ProcessingError,
            BuildPreviewUrl(receipt.Id),
            receipt.CreatedTransactionId.HasValue,
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
        await _receiptProcessingQueue.QueueAsync(receipt.Id, ct);

        var details = new ReceiptDetailsDto(
            receipt.Id,
            receipt.OriginalFileName,
            receipt.ContentType,
            receipt.FileSizeBytes,
            receipt.UploadedAt,
            receipt.OcrStatus,
            receipt.RecognizedTotalAmount,
            null,
            receipt.RecognizedDate,
            receipt.RecognizedMerchant,
            receipt.ProcessingError,
            BuildPreviewUrl(receipt.Id),
            receipt.CreatedTransactionId.HasValue,
            []);

        return Result<ReceiptDetailsDto>.Success(details);
    }

    public async Task<Result<ReceiptDetailsDto>> RetryAsync(Guid receiptId, CancellationToken ct = default)
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

        receipt.OcrStatus = ReceiptOcrStatus.Pending;
        receipt.ProcessingError = null;
        receipt.RecognizedMerchant = null;
        receipt.RecognizedDate = null;
        receipt.RecognizedTotalAmount = null;
        receipt.RawOcrData = null;
        receipt.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        await _receiptProcessingQueue.QueueAsync(receipt.Id, ct);

        return Result<ReceiptDetailsDto>.Success(new ReceiptDetailsDto(
            receipt.Id,
            receipt.OriginalFileName,
            receipt.ContentType,
            receipt.FileSizeBytes,
            receipt.UploadedAt,
            receipt.OcrStatus,
            receipt.RecognizedTotalAmount,
            null,
            receipt.RecognizedDate,
            receipt.RecognizedMerchant,
            receipt.ProcessingError,
            BuildPreviewUrl(receipt.Id),
            receipt.CreatedTransactionId.HasValue,
            []));
    }

    public async Task<Result> DeleteAsync(Guid receiptId, CancellationToken ct = default)
    {
        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result.Failure(user.Error);
        }

        var receipt = _dbContext.Receipts.FirstOrDefault(entity => entity.Id == receiptId && entity.UserId == user.Value.Id);
        if (receipt is null)
        {
            return Result.Failure(AppErrors.NotFound("Receipt not found."));
        }

        var receiptItems = _dbContext.ReceiptItems
            .Where(item => item.ReceiptId == receipt.Id)
            .ToList();

        foreach (var receiptItem in receiptItems)
        {
            _dbContext.Remove(receiptItem);
        }

        _dbContext.Remove(receipt);
        await _dbContext.SaveChangesAsync(ct);

        await _receiptFileStorage.DeleteAsync(receipt.StorageContainer, receipt.FileUrl, ct);

        return Result.Success();
    }

    public async Task<Result<ReceiptItemDto>> UpdateItemAsync(
        Guid receiptItemId,
        string name,
        decimal price,
        string currencyCode,
        Guid? mappedCategoryId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<ReceiptItemDto>.Failure(AppErrors.Validation("Product name is required."));
        }

        if (price <= 0)
        {
            return Result<ReceiptItemDto>.Failure(AppErrors.Validation("Price must be positive."));
        }

        if (!CurrencyCodeNormalizer.TryNormalize(currencyCode, out var normalizedCurrencyCode))
        {
            return Result<ReceiptItemDto>.Failure(AppErrors.Validation("Currency code is invalid."));
        }

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

        if (receipt.CreatedTransactionId.HasValue)
        {
            return Result<ReceiptItemDto>.Failure(AppErrors.Conflict("Receipt has already been applied to an account."));
        }

        var resolvedCategory = await ResolveCategoryAsync(user.Value.Id, mappedCategoryId, ct);
        if (resolvedCategory.IsFailure || resolvedCategory.Value is null)
        {
            return Result<ReceiptItemDto>.Failure(resolvedCategory.Error);
        }

        receiptItem.Name = name.Trim();
        receiptItem.Price = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
        receiptItem.CurrencyCode = normalizedCurrencyCode;
        receiptItem.MappedCategoryId = resolvedCategory.Value.Id;
        receiptItem.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        return Result<ReceiptItemDto>.Success(new ReceiptItemDto(
            receiptItem.Id,
            receiptItem.Name,
            receiptItem.Price,
            receiptItem.CurrencyCode,
            receiptItem.CategoryName,
            receiptItem.MappedCategoryId,
            resolvedCategory.Value.Name,
            receiptItem.SortOrder));
    }

    public async Task<Result<ReceiptItemDto>> CreateItemAsync(
        Guid receiptId,
        string name,
        decimal price,
        string currencyCode,
        Guid? mappedCategoryId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<ReceiptItemDto>.Failure(AppErrors.Validation("Product name is required."));
        }

        if (price <= 0)
        {
            return Result<ReceiptItemDto>.Failure(AppErrors.Validation("Price must be positive."));
        }

        if (!CurrencyCodeNormalizer.TryNormalize(currencyCode, out var normalizedCurrencyCode))
        {
            return Result<ReceiptItemDto>.Failure(AppErrors.Validation("Currency code is invalid."));
        }

        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result<ReceiptItemDto>.Failure(user.Error);
        }

        var receipt = _dbContext.Receipts.FirstOrDefault(entity => entity.Id == receiptId && entity.UserId == user.Value.Id);
        if (receipt is null)
        {
            return Result<ReceiptItemDto>.Failure(AppErrors.NotFound("Receipt not found."));
        }

        if (receipt.CreatedTransactionId.HasValue)
        {
            return Result<ReceiptItemDto>.Failure(AppErrors.Conflict("Receipt has already been applied to an account."));
        }

        var resolvedCategory = await ResolveCategoryAsync(user.Value.Id, mappedCategoryId, ct);
        if (resolvedCategory.IsFailure || resolvedCategory.Value is null)
        {
            return Result<ReceiptItemDto>.Failure(resolvedCategory.Error);
        }

        var sortOrder = _dbContext.ReceiptItems
            .Where(item => item.ReceiptId == receipt.Id)
            .Select(item => (int?)item.SortOrder)
            .Max() ?? -1;

        var receiptItem = new ReceiptItem
        {
            Id = Guid.NewGuid(),
            ReceiptId = receipt.Id,
            Name = name.Trim(),
            Price = decimal.Round(price, 2, MidpointRounding.AwayFromZero),
            CurrencyCode = normalizedCurrencyCode,
            CategoryName = resolvedCategory.Value.Name,
            MappedCategoryId = resolvedCategory.Value.Id,
            SortOrder = sortOrder + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _dbContext.AddAsync(receiptItem, ct);
        receipt.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        return Result<ReceiptItemDto>.Success(new ReceiptItemDto(
            receiptItem.Id,
            receiptItem.Name,
            receiptItem.Price,
            receiptItem.CurrencyCode,
            receiptItem.CategoryName,
            receiptItem.MappedCategoryId,
            resolvedCategory.Value.Name,
            receiptItem.SortOrder));
    }

    public async Task<Result> DeleteItemAsync(Guid receiptItemId, CancellationToken ct = default)
    {
        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result.Failure(user.Error);
        }

        var receiptItem = _dbContext.ReceiptItems.FirstOrDefault(item => item.Id == receiptItemId);
        if (receiptItem is null)
        {
            return Result.Failure(AppErrors.NotFound("Receipt item not found."));
        }

        var receipt = _dbContext.Receipts.FirstOrDefault(entity => entity.Id == receiptItem.ReceiptId && entity.UserId == user.Value.Id);
        if (receipt is null)
        {
            return Result.Failure(AppErrors.NotFound("Receipt not found."));
        }

        if (receipt.CreatedTransactionId.HasValue)
        {
            return Result.Failure(AppErrors.Conflict("Receipt has already been applied to an account."));
        }

        _dbContext.Remove(receiptItem);
        receipt.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<ReceiptApplyResult>> ApplyToAccountAsync(
        Guid receiptId,
        Guid accountId,
        DateTime? transactionDateOverride,
        CancellationToken ct = default)
    {
        var user = await _authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null)
        {
            return Result<ReceiptApplyResult>.Failure(user.Error);
        }

        var receipt = _dbContext.Receipts.FirstOrDefault(entity => entity.Id == receiptId && entity.UserId == user.Value.Id);
        if (receipt is null)
        {
            return Result<ReceiptApplyResult>.Failure(AppErrors.NotFound("Receipt not found."));
        }

        if (receipt.CreatedTransactionId.HasValue)
        {
            return Result<ReceiptApplyResult>.Failure(AppErrors.Conflict("Receipt has already been applied to an account."));
        }

        var account = _dbContext.Accounts.FirstOrDefault(entity => entity.Id == accountId && entity.UserId == user.Value.Id);
        if (account is null)
        {
            return Result<ReceiptApplyResult>.Failure(AppErrors.NotFound("Account not found."));
        }

        var receiptItems = _dbContext.ReceiptItems
            .Where(item => item.ReceiptId == receipt.Id)
            .OrderBy(item => item.SortOrder)
            .ToList();

        if (receiptItems.Count == 0)
        {
            return Result<ReceiptApplyResult>.Failure(AppErrors.Validation("Receipt does not contain any parsed items."));
        }

        var categoryLookup = await EnsureCategoryLookupAsync(user.Value.Id, ct);
        var transactionDate = ResolveTransactionDate(receipt, transactionDateOverride);
        var newTransactions = new List<Transaction>();
        var affectedCategoryIds = new HashSet<Guid>();

        foreach (var receiptItem in receiptItems)
        {
            if (!CurrencyCodeNormalizer.TryNormalize(receiptItem.CurrencyCode, out var normalizedCurrencyCode))
            {
                return Result<ReceiptApplyResult>.Failure(AppErrors.Validation($"Receipt item '{receiptItem.Name}' has invalid currency code."));
            }

            var mappedCategoryId = receiptItem.MappedCategoryId;
            if (!mappedCategoryId.HasValue || !categoryLookup.TryGetValue(mappedCategoryId.Value, out var category))
            {
                return Result<ReceiptApplyResult>.Failure(AppErrors.Validation($"Receipt item '{receiptItem.Name}' has invalid category mapping."));
            }

            var normalizedAmountResult = await NormalizeAmountForAccountAsync(
                receiptItem.Price,
                normalizedCurrencyCode,
                account.CurrencyCode,
                ct);

            if (normalizedAmountResult.IsFailure)
            {
                return Result<ReceiptApplyResult>.Failure(normalizedAmountResult.Error);
            }

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = user.Value.Id,
                AccountId = account.Id,
                CategoryId = mappedCategoryId,
                Type = TransactionType.Expense,
                Amount = normalizedAmountResult.Value,
                CurrencyCode = account.CurrencyCode.Trim().ToUpperInvariant(),
                TransactionDate = transactionDate,
                Description = receiptItem.Name,
                Source = TransactionSource.Receipt,
                Status = TransactionStatus.Posted,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            account.CurrentBalance -= transaction.Amount;
            account.UpdatedAt = DateTime.UtcNow;
            newTransactions.Add(transaction);
            affectedCategoryIds.Add(mappedCategoryId.Value);
        }

        foreach (var transaction in newTransactions)
        {
            await _dbContext.AddAsync(transaction, ct);
        }

        receipt.CreatedTransactionId = newTransactions[0].Id;
        receipt.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        await _budgetService.EvaluateBudgetNotificationsAsync(user.Value.Id, affectedCategoryIds.Cast<Guid?>(), [account.Id], ct);

        return Result<ReceiptApplyResult>.Success(new ReceiptApplyResult(receipt.Id, account.Id, newTransactions.Count));
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

        if (receipt.CreatedTransactionId.HasValue)
        {
            return Result<ReceiptItemDto>.Failure(AppErrors.Conflict("Receipt has already been applied to an account."));
        }

        var resolvedCategory = await ResolveCategoryAsync(user.Value.Id, mappedCategoryId, ct);
        if (resolvedCategory.IsFailure || resolvedCategory.Value is null)
        {
            return Result<ReceiptItemDto>.Failure(resolvedCategory.Error);
        }

        receiptItem.MappedCategoryId = resolvedCategory.Value.Id;
        receiptItem.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        return Result<ReceiptItemDto>.Success(new ReceiptItemDto(
            receiptItem.Id,
            receiptItem.Name,
            receiptItem.Price,
            receiptItem.CurrencyCode,
            receiptItem.CategoryName,
            receiptItem.MappedCategoryId,
            resolvedCategory.Value.Name,
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

    private static ReceiptListItemDto MapListItem(Receipt receipt, string? recognizedCurrencyCode)
    {
        return new ReceiptListItemDto(
            receipt.Id,
            receipt.OriginalFileName,
            receipt.ContentType,
            receipt.FileSizeBytes,
            receipt.UploadedAt,
            receipt.OcrStatus,
            receipt.RecognizedTotalAmount,
            recognizedCurrencyCode,
            receipt.RecognizedDate,
            receipt.RecognizedMerchant,
            receipt.ProcessingError,
            BuildPreviewUrl(receipt.Id),
            receipt.CreatedTransactionId.HasValue);
    }

    private async Task<Result<Category>> ResolveCategoryAsync(Guid userId, Guid? mappedCategoryId, CancellationToken ct)
    {
        if (mappedCategoryId.HasValue)
        {
            var category = _dbContext.Categories.FirstOrDefault(category =>
                category.Id == mappedCategoryId.Value
                && category.IsActive
                && (category.UserId == userId || category.IsSystem));

            if (category is null)
            {
                return Result<Category>.Failure(AppErrors.Validation("Category not found."));
            }

            return Result<Category>.Success(category);
        }

        var othersCategory = await EnsureOthersCategoryAsync(userId, ct);
        return Result<Category>.Success(othersCategory);
    }

    private async Task<Category> EnsureOthersCategoryAsync(Guid userId, CancellationToken ct)
    {
        var othersCategory = _dbContext.Categories.FirstOrDefault(category =>
            category.UserId == userId
            && category.Type == CategoryType.Expense
            && category.IsActive
            && category.Name == "Others");

        if (othersCategory is not null)
        {
            return othersCategory;
        }

        othersCategory = new Category
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Others",
            Type = CategoryType.Expense,
            IsSystem = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _dbContext.AddAsync(othersCategory, ct);
        await _dbContext.SaveChangesAsync(ct);
        return othersCategory;
    }

    private async Task<Dictionary<Guid, Category>> EnsureCategoryLookupAsync(Guid userId, CancellationToken ct)
    {
        await EnsureOthersCategoryAsync(userId, ct);
        return _dbContext.Categories
            .Where(category => category.IsActive && (category.UserId == userId || category.IsSystem))
            .ToDictionary(category => category.Id, category => category);
    }

    private async Task<Result<decimal>> NormalizeAmountForAccountAsync(
        decimal amount,
        string transactionCurrencyCode,
        string accountCurrencyCode,
        CancellationToken ct)
    {
        var sourceCurrency = transactionCurrencyCode.Trim().ToUpperInvariant();
        var targetCurrency = accountCurrencyCode.Trim().ToUpperInvariant();

        if (sourceCurrency == targetCurrency)
        {
            return Result<decimal>.Success(decimal.Round(amount, 2, MidpointRounding.AwayFromZero));
        }

        var convertedAmountResult = await _currencyRateProvider.ConvertAsync(amount, sourceCurrency, targetCurrency, ct);
        if (convertedAmountResult.IsFailure)
        {
            return Result<decimal>.Failure(convertedAmountResult.Error);
        }

        return Result<decimal>.Success(convertedAmountResult.Value);
    }

    private static string BuildPreviewUrl(Guid receiptId)
    {
        return $"/receipts/{receiptId}/file";
    }

    private static DateTime ResolveTransactionDate(Receipt receipt, DateTime? transactionDateOverride)
    {
        if (transactionDateOverride.HasValue)
        {
            return transactionDateOverride.Value;
        }

        return receipt.UploadedAt;
    }

    private static string? ResolveReceiptCurrencyCode(IReadOnlyCollection<ReceiptItem> receiptItems)
    {
        return receiptItems
            .Where(item => !string.IsNullOrWhiteSpace(item.CurrencyCode))
            .OrderBy(item => item.SortOrder)
            .Select(item => item.CurrencyCode.Trim().ToUpperInvariant())
            .FirstOrDefault();
    }
}
