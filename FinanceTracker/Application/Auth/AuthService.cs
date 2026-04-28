using Application.Abstractions;
using Domain.Users;
using Shared.Constants;
using Shared.Errors;
using Shared.Results;

namespace Application.Auth;

public sealed class AuthService(
    IFinanceDbContext dbContext,
    ICurrentUserAccessor currentUser,
    IIdentityAuthClient identityAuthClient) : IAuthService
{
    public async Task<Result<User>> GetOrSyncCurrentUserAsync(CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            return Result<User>.Failure(new AppError(ErrorCodes.Unauthorized, "User is not authenticated."));
        }

        if (string.IsNullOrWhiteSpace(currentUser.KeycloakUserId))
        {
            return Result<User>.Failure(new AppError(ErrorCodes.Unauthorized, "Missing subject (sub) in access token."));
        }

        var now = DateTime.UtcNow;
        var normalizedEmail = NormalizeEmail(currentUser.Email);
        var normalizedUsername = NormalizeUsername(currentUser.Username);

        var user = ResolveCurrentUserByKeycloakId(currentUser.KeycloakUserId);

        if (user is null)
        {
            var username = BuildUniqueUsername(normalizedUsername ?? currentUser.KeycloakUserId);
            var email = BuildUniqueEmail(normalizedEmail ?? $"{currentUser.KeycloakUserId}@local.invalid", Guid.Empty);

            user = new User
            {
                KeycloakUserId = currentUser.KeycloakUserId,
                Username = username,
                Email = email,
                RegisteredAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };

            await dbContext.AddAsync(user, ct);
        }
        else
        {
            var changed = TryApplyIdentityFields(user, normalizedEmail, normalizedUsername);
            if (changed)
            {
                user.UpdatedAt = now;
            }
        }

        await dbContext.SaveChangesAsync(ct);
        return Result<User>.Success(user);
    }

    public async Task<Result<AuthTokenDto>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var registerResult = await identityAuthClient.RegisterUserAsync(request, ct);
        if (registerResult.IsFailure)
        {
            return Result<AuthTokenDto>.Failure(registerResult.Error);
        }

        var tokenResult = await identityAuthClient.LoginAsync(new LoginRequest(request.Email, request.Password), ct);
        if (tokenResult.IsFailure || tokenResult.Value is null)
        {
            return Result<AuthTokenDto>.Failure(tokenResult.Error);
        }

        var syncResult = await SyncLocalUserByAccessTokenAsync(tokenResult.Value.AccessToken, ct);
        if (syncResult.IsFailure)
        {
            return Result<AuthTokenDto>.Failure(syncResult.Error);
        }

        return Result<AuthTokenDto>.Success(tokenResult.Value);
    }

    public async Task<Result<AuthTokenDto>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var tokenResult = await identityAuthClient.LoginAsync(request, ct);
        if (tokenResult.IsFailure || tokenResult.Value is null)
        {
            return Result<AuthTokenDto>.Failure(tokenResult.Error);
        }

        var syncResult = await SyncLocalUserByAccessTokenAsync(tokenResult.Value.AccessToken, ct);
        if (syncResult.IsFailure)
        {
            return Result<AuthTokenDto>.Failure(syncResult.Error);
        }

        return Result<AuthTokenDto>.Success(tokenResult.Value);
    }

    public async Task<Result<AuthTokenDto>> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result<AuthTokenDto>.Failure(new AppError(ErrorCodes.Validation, "Refresh token is required."));
        }

        return await identityAuthClient.RefreshAsync(request.RefreshToken, ct);
    }

    private async Task<Result<User>> SyncLocalUserByAccessTokenAsync(string accessToken, CancellationToken ct)
    {
        var userInfoResult = await identityAuthClient.GetUserInfoAsync(accessToken, ct);
        if (userInfoResult.IsFailure || userInfoResult.Value is null)
        {
            return Result<User>.Failure(userInfoResult.Error);
        }

        var userInfo = userInfoResult.Value;
        var now = DateTime.UtcNow;

        var normalizedEmail = NormalizeEmail(userInfo.Email);
        var normalizedUsername = NormalizeUsername(
            string.IsNullOrWhiteSpace(userInfo.PreferredUsername) ? userInfo.Email : userInfo.PreferredUsername);

        if (string.IsNullOrWhiteSpace(userInfo.Subject))
        {
            return Result<User>.Failure(new AppError(ErrorCodes.Unauthorized, "Keycloak subject (sub) is missing."));
        }

        var user = ResolveCurrentUserByKeycloakId(userInfo.Subject);

        if (user is null)
        {
            var username = BuildUniqueUsername(normalizedUsername ?? userInfo.Subject);
            var email = BuildUniqueEmail(normalizedEmail ?? $"{userInfo.Subject}@local.invalid", Guid.Empty);

            user = new User
            {
                KeycloakUserId = userInfo.Subject,
                Username = username,
                Email = email,
                FirstName = userInfo.GivenName,
                LastName = userInfo.FamilyName,
                RegisteredAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };

            await dbContext.AddAsync(user, ct);
        }
        else
        {
            var changed = TryApplyIdentityFields(user, normalizedEmail, normalizedUsername);
            var namesChanged = !string.Equals(user.FirstName, userInfo.GivenName, StringComparison.Ordinal)
                               || !string.Equals(user.LastName, userInfo.FamilyName, StringComparison.Ordinal);

            user.FirstName = userInfo.GivenName;
            user.LastName = userInfo.FamilyName;

            if (changed || namesChanged)
            {
                user.UpdatedAt = now;
            }
        }

        await dbContext.SaveChangesAsync(ct);
        return Result<User>.Success(user);
    }

    private bool TryApplyIdentityFields(User user, string? email, string? username)
    {
        var changed = false;

        if (!string.IsNullOrWhiteSpace(username)
            && !string.Equals(user.Username, username, StringComparison.Ordinal)
            && IsUsernameFreeForUser(username, user.Id))
        {
            user.Username = username;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(email)
            && !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)
            && IsEmailFreeForUser(email, user.Id))
        {
            user.Email = email;
            changed = true;
        }

        return changed;
    }

    private bool IsEmailFreeForUser(string email, Guid currentUserId)
    {
        return !dbContext.Users.Any(u => u.Id != currentUserId && u.Email == email);
    }

    private bool IsUsernameFreeForUser(string username, Guid currentUserId)
    {
        return !dbContext.Users.Any(u => u.Id != currentUserId && u.Username == username);
    }

    private string BuildUniqueEmail(string baseEmail, Guid currentUserId)
    {
        var normalized = NormalizeEmail(baseEmail) ?? $"{Guid.NewGuid():N}@local.invalid";
        if (IsEmailFreeForUser(normalized, currentUserId))
        {
            return normalized;
        }

        var atIndex = normalized.IndexOf('@');
        var localPart = atIndex > 0 ? normalized[..atIndex] : normalized;
        var domainPart = atIndex > 0 ? normalized[atIndex..] : "@local.invalid";

        var attempt = 1;
        while (attempt < 5000)
        {
            var candidate = $"{localPart}+{attempt}{domainPart}";
            if (IsEmailFreeForUser(candidate, currentUserId))
            {
                return candidate;
            }

            attempt++;
        }

        return $"{Guid.NewGuid():N}@local.invalid";
    }

    private string BuildUniqueUsername(string baseUsername)
    {
        var normalized = NormalizeUsername(baseUsername) ?? $"user-{Guid.NewGuid():N}";
        if (IsUsernameFreeForUser(normalized, Guid.Empty))
        {
            return normalized;
        }

        var attempt = 1;
        while (attempt < 5000)
        {
            var candidate = $"{normalized}_{attempt}";
            if (IsUsernameFreeForUser(candidate, Guid.Empty))
            {
                return candidate;
            }

            attempt++;
        }

        return $"user-{Guid.NewGuid():N}";
    }

    private static string? NormalizeEmail(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeUsername(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private User? ResolveCurrentUserByKeycloakId(string keycloakUserId)
    {
        return dbContext.Users.FirstOrDefault(u => u.KeycloakUserId == keycloakUserId);
    }
}
