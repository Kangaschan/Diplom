using System.Text.Json;
using Application.Abstractions;
using Domain.Categories;
using Domain.Common;
using Domain.Receipts;
using Shared.Results;

namespace Application.Receipts;

public sealed class ReceiptProcessingService
{
    private readonly IFinanceDbContext _dbContext;
    private readonly IReceiptFileStorage _receiptFileStorage;
    private readonly IReceiptOcrClient _receiptOcrClient;

    public ReceiptProcessingService(
        IFinanceDbContext dbContext,
        IReceiptFileStorage receiptFileStorage,
        IReceiptOcrClient receiptOcrClient)
    {
        _dbContext = dbContext;
        _receiptFileStorage = receiptFileStorage;
        _receiptOcrClient = receiptOcrClient;
    }

    public async Task ProcessAsync(Guid receiptId, CancellationToken ct = default)
    {
        var receipt = _dbContext.Receipts.FirstOrDefault(entity => entity.Id == receiptId);
        if (receipt is null)
        {
            return;
        }

        receipt.OcrStatus = ReceiptOcrStatus.Pending;
        receipt.ProcessingError = null;
        receipt.RecognizedMerchant = null;
        receipt.RecognizedDate = null;
        receipt.RecognizedTotalAmount = null;
        receipt.RawOcrData = null;
        receipt.UpdatedAt = DateTime.UtcNow;
        ClearReceiptItems(receipt.Id);
        await _dbContext.SaveChangesAsync(ct);

        var file = await _receiptFileStorage.OpenReadAsync(receipt.StorageContainer, receipt.FileUrl, ct);
        if (file is null)
        {
            await MarkFailedAsync(receipt, "Receipt file not found in storage.", receipt.RawOcrData, ct);
            return;
        }

        var categories = await GetUserReceiptCategoriesAsync(receipt.UserId, ct);
        var parseResult = await _receiptOcrClient.ParseReceiptAsync(
            file.Content,
            receipt.OriginalFileName,
            receipt.ContentType,
            categories.Select(category => category.Name).ToList(),
            ct);

        if (parseResult.IsFailure || parseResult.Value is null)
        {
            await MarkFailedAsync(
                receipt,
                parseResult.Error.Message,
                BuildFailureRawData(receipt.RawOcrData, parseResult.Error.Message),
                ct);
            return;
        }

        var completionResult = await ApplyParseResultAsync(receipt, categories, parseResult.Value, ct);
        if (completionResult.IsFailure)
        {
            await MarkFailedAsync(
                receipt,
                completionResult.Error.Message,
                BuildFailureRawData(parseResult.Value.RawResponse, completionResult.Error.Message, parseResult.Value.UploadedFileId, parseResult.Value.Prompt),
                ct);
        }
    }

    private async Task<Result> ApplyParseResultAsync(
        Receipt receipt,
        IReadOnlyCollection<Category> categories,
        ReceiptOcrParseResult parseResult,
        CancellationToken ct)
    {
        if (parseResult.Items.Count == 0)
        {
            return Result.Failure(AppErrors.Validation("OCR did not return any receipt items."));
        }

        var categoryLookup = categories.ToDictionary(
            category => NormalizeName(category.Name),
            category => category,
            StringComparer.OrdinalIgnoreCase);

        if (!categoryLookup.TryGetValue(NormalizeName("Others"), out var othersCategory))
        {
            return Result.Failure(AppErrors.Validation("Category Others is missing."));
        }

        var newItems = new List<ReceiptItem>();
        var sortOrder = 1;

        foreach (var item in parseResult.Items)
        {
            var normalizedCategoryName = NormalizeName(item.CategoryName);
            var mappedCategory = categoryLookup.TryGetValue(normalizedCategoryName, out var resolvedCategory)
                ? resolvedCategory
                : othersCategory;

            newItems.Add(new ReceiptItem
            {
                Id = Guid.NewGuid(),
                ReceiptId = receipt.Id,
                Name = item.Name.Trim(),
                CurrencyCode = item.CurrencyCode.Trim().ToUpperInvariant(),
                Price = item.Price,
                CategoryName = item.CategoryName.Trim(),
                MappedCategoryId = mappedCategory.Id,
                SortOrder = sortOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            sortOrder++;
        }

        foreach (var item in newItems)
        {
            await _dbContext.AddAsync(item, ct);
        }

        receipt.RecognizedMerchant = string.IsNullOrWhiteSpace(parseResult.Merchant) ? null : parseResult.Merchant.Trim();
        receipt.RecognizedDate = parseResult.PurchaseDate;
        receipt.RecognizedTotalAmount = parseResult.TotalAmount;
        receipt.OcrStatus = ReceiptOcrStatus.Completed;
        receipt.ProcessingError = null;
        receipt.RawOcrData = JsonSerializer.Serialize(new
        {
            uploadedFileId = parseResult.UploadedFileId,
            prompt = parseResult.Prompt,
            totalCurrencyCode = parseResult.TotalCurrencyCode,
            rawResponse = parseResult.RawResponse
        });
        receipt.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<IReadOnlyCollection<Category>> GetUserReceiptCategoriesAsync(Guid userId, CancellationToken ct)
    {
        var userCategories = _dbContext.Categories
            .Where(category =>
                category.UserId == userId
                && category.Type == CategoryType.Expense
                && category.IsActive)
            .ToList();

        var othersCategory = userCategories.FirstOrDefault(category => NormalizeName(category.Name) == NormalizeName("Others"));
        if (othersCategory is null)
        {
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
            userCategories.Add(othersCategory);
        }

        return userCategories
            .GroupBy(category => NormalizeName(category.Name), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(category => category.Name)
            .ToList();
    }

    private void ClearReceiptItems(Guid receiptId)
    {
        var items = _dbContext.ReceiptItems.Where(item => item.ReceiptId == receiptId).ToList();
        foreach (var item in items)
        {
            _dbContext.Remove(item);
        }
    }

    private async Task MarkFailedAsync(Receipt receipt, string message, string? rawOcrData, CancellationToken ct)
    {
        receipt.OcrStatus = ReceiptOcrStatus.Failed;
        receipt.ProcessingError = message;
        receipt.RawOcrData = rawOcrData;
        receipt.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
    }

    private static string NormalizeName(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static string BuildFailureRawData(string? rawResponse, string message, string? uploadedFileId = null, string? prompt = null)
    {
        return JsonSerializer.Serialize(new
        {
            uploadedFileId,
            prompt,
            rawResponse,
            error = message
        });
    }
}
