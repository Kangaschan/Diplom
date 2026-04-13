using Domain.Users;
using Shared.Results;

namespace Application.Auth;

public sealed record RegisterRequest(string Username, string Email, string Password, string? FirstName, string? LastName);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);

public sealed record AuthTokenDto(string AccessToken, string RefreshToken, int ExpiresIn, string TokenType);
public sealed record KeycloakUserInfo(string Subject, string Email, string PreferredUsername, string? GivenName, string? FamilyName);

public interface IIdentityAuthClient
{
    Task<Result> RegisterUserAsync(RegisterRequest request, CancellationToken ct = default);
    Task<Result<AuthTokenDto>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<Result<AuthTokenDto>> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task<Result<KeycloakUserInfo>> GetUserInfoAsync(string accessToken, CancellationToken ct = default);
}

public interface IAuthService
{
    Task<Result<User>> GetOrSyncCurrentUserAsync(CancellationToken ct = default);
    Task<Result<AuthTokenDto>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<Result<AuthTokenDto>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<Result<AuthTokenDto>> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
}
