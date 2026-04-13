using Application.Abstractions;
using Domain.Users;
using Shared.Results;

namespace Application.Users;

public sealed class ProfileService(IFinanceDbContext dbContext, ICurrentUserAccessor currentUser, Auth.IAuthService authService) : IProfileService
{
    public async Task<Result<User>> GetCurrentProfileAsync(CancellationToken ct = default)
    {
        return await authService.GetOrSyncCurrentUserAsync(ct);
    }

    public async Task<Result<User>> UpdateProfileAsync(
        string? firstName,
        string? lastName,
        string? avatarUrl,
        string? username,
        string? email,
        CancellationToken ct = default)
    {
        var userResult = await authService.GetOrSyncCurrentUserAsync(ct);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<User>.Failure(userResult.Error);
        }

        var user = userResult.Value;

        if (!string.IsNullOrWhiteSpace(username) && dbContext.Users.Any(u => u.Username == username && u.Id != user.Id))
        {
            return Result<User>.Failure(AppErrors.Conflict("Username is already taken."));
        }

        if (!string.IsNullOrWhiteSpace(email) && dbContext.Users.Any(u => u.Email == email && u.Id != user.Id))
        {
            return Result<User>.Failure(AppErrors.Conflict("Email is already taken."));
        }

        user.FirstName = firstName?.Trim();
        user.LastName = lastName?.Trim();
        user.AvatarUrl = avatarUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(username)) user.Username = username.Trim();
        if (!string.IsNullOrWhiteSpace(email)) user.Email = email.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(ct);
        return Result<User>.Success(user);
    }
}
