using Application.Abstractions;
using Application.Auth;
using Domain.Categories;
using Domain.Common;
using Shared.Results;

namespace Application.Categories;

public sealed class CategoryService(IFinanceDbContext dbContext, IAuthService authService)
{
    public async Task<Result<IReadOnlyCollection<Category>>> GetAvailableAsync(CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<IReadOnlyCollection<Category>>.Failure(user.Error);

        var categories = dbContext.Categories
            .Where(c => c.IsActive && (c.IsSystem || c.UserId == user.Value.Id))
            .OrderBy(c => c.Name)
            .ToList();

        return Result<IReadOnlyCollection<Category>>.Success(categories);
    }

    public async Task<Result<Category>> CreateAsync(string name, CategoryType type, CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<Category>.Failure(user.Error);

        var category = new Category
        {
            UserId = user.Value.Id,
            Name = name.Trim(),
            Type = type,
            IsSystem = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await dbContext.AddAsync(category, ct);
        await dbContext.SaveChangesAsync(ct);

        return Result<Category>.Success(category);
    }

    public async Task<Result<Category>> UpdateAsync(Guid id, string name, bool isActive, CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result<Category>.Failure(user.Error);

        var category = dbContext.Categories.FirstOrDefault(c => c.Id == id && c.UserId == user.Value.Id);
        if (category is null) return Result<Category>.Failure(AppErrors.NotFound("Category not found."));

        category.Name = name.Trim();
        category.IsActive = isActive;
        category.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return Result<Category>.Success(category);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await authService.GetOrSyncCurrentUserAsync(ct);
        if (user.IsFailure || user.Value is null) return Result.Failure(user.Error);

        var category = dbContext.Categories.FirstOrDefault(c => c.Id == id && c.UserId == user.Value.Id);
        if (category is null) return Result.Failure(AppErrors.NotFound("Category not found."));

        var isUsed = dbContext.Transactions.Any(t => t.CategoryId == category.Id);
        if (isUsed)
        {
            return Result.Failure(AppErrors.Conflict("Category is used by transactions."));
        }

        dbContext.Remove(category);
        await dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }
}
