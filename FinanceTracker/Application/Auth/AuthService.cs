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

        var now = DateTime.UtcNow;
        var user = ResolveCurrentUser(currentUser.KeycloakUserId, currentUser.Email, currentUser.Username);

        if (user is null)
        {
            user = new User
            {
                KeycloakUserId = currentUser.KeycloakUserId,
                Username = currentUser.Username,
                Email = currentUser.Email,
                RegisteredAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };

            await dbContext.AddAsync(user, ct);
        }
        else
        {
            user.KeycloakUserId = currentUser.KeycloakUserId;

            if (!string.IsNullOrWhiteSpace(currentUser.Username))
            {
                user.Username = currentUser.Username;
            }

            if (!string.IsNullOrWhiteSpace(currentUser.Email))
            {
                user.Email = currentUser.Email;
            }

            user.UpdatedAt = now;
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
        var candidateUsername = string.IsNullOrWhiteSpace(userInfo.PreferredUsername)
            ? userInfo.Email
            : userInfo.PreferredUsername;

        var user = ResolveCurrentUser(userInfo.Subject, userInfo.Email, candidateUsername);

        if (user is null)
        {
            user = new User
            {
                KeycloakUserId = userInfo.Subject,
                Username = candidateUsername,
                Email = userInfo.Email,
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
            user.KeycloakUserId = userInfo.Subject;
            user.Username = candidateUsername;
            user.Email = userInfo.Email;
            user.FirstName = userInfo.GivenName;
            user.LastName = userInfo.FamilyName;
            user.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(ct);
        return Result<User>.Success(user);
    }

    private User? ResolveCurrentUser(string keycloakUserId, string? email, string? username)
    {
        var user = dbContext.Users.FirstOrDefault(u => u.KeycloakUserId == keycloakUserId);
        if (user is not null)
        {
            return user;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            user = dbContext.Users.FirstOrDefault(u => u.Email == email);
            if (user is not null)
            {
                return user;
            }
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            user = dbContext.Users.FirstOrDefault(u => u.Username == username);
            if (user is not null)
            {
                return user;
            }
        }

        return null;
    }
}
