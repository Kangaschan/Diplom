using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using Application.Auth;
using Identity.Models;
using Shared.Constants;
using Shared.Errors;
using Shared.Results;
using Microsoft.Extensions.Options;

namespace Identity.Services;

public sealed class KeycloakServiceClient(IOptions<KeycloakOptions> options, HttpClient httpClient) : IIdentityAuthClient
{
    private readonly KeycloakOptions _options = options.Value;
    private readonly HttpClient _httpClient = httpClient;

    private string TokenRoute => $"{_options.Authority}/protocol/openid-connect/token";
    private string AdminTokenRoute => $"{_options.BaseUrl.TrimEnd('/')}/realms/master/protocol/openid-connect/token";
    private string UserInfoRoute => $"{_options.Authority}/protocol/openid-connect/userinfo";
    private string AdminUsersRoute => $"{_options.BaseUrl.TrimEnd('/')}/admin/realms/{_options.Realm}/users";

    public async Task<Result> RegisterUserAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var adminToken = await GetAdminTokenAsync(ct);
        if (adminToken.IsFailure || string.IsNullOrWhiteSpace(adminToken.Value))
        {
            return Result.Failure(adminToken.Error);
        }

        var payload = new
        {
            username = request.Username,
            email = request.Email,
            enabled = true,
            firstName = request.FirstName,
            lastName = request.LastName,
            emailVerified = true,
            credentials = new[]
            {
                new
                {
                    type = "password",
                    value = request.Password,
                    temporary = false
                }
            }
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, AdminUsersRoute)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken.Value);

        using var response = await _httpClient.SendAsync(message, ct);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return Result.Failure(new AppError(ErrorCodes.Conflict, "User already exists."));
        }

        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync(ct);
            return Result.Failure(new AppError(ErrorCodes.Unexpected, $"Keycloak registration failed: {(int)response.StatusCode}. {details}"));
        }

        return Result.Success();
    }

    public async Task<Result<AuthTokenDto>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _options.ClientId,
            ["scope"] = "openid profile email",
            ["username"] = request.Email,
            ["password"] = request.Password
        };

        if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            body["client_secret"] = _options.ClientSecret;
        }

        using var response = await _httpClient.PostAsync(TokenRoute, new FormUrlEncodedContent(body), ct);
        return await ReadTokenResponseAsync(response, ct);
    }

    public async Task<Result<AuthTokenDto>> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _options.ClientId,
            ["refresh_token"] = refreshToken
        };

        if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            body["client_secret"] = _options.ClientSecret;
        }

        using var response = await _httpClient.PostAsync(TokenRoute, new FormUrlEncodedContent(body), ct);
        return await ReadTokenResponseAsync(response, ct);
    }

    public async Task<Result<KeycloakUserInfo>> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, UserInfoRoute);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(message, ct);
        if (response.IsSuccessStatusCode)
        {
            var userInfo = await response.Content.ReadFromJsonAsync<KeycloakUserInfoResponse>(cancellationToken: ct);
            if (userInfo is not null && !string.IsNullOrWhiteSpace(userInfo.Sub) && !string.IsNullOrWhiteSpace(userInfo.Email))
            {
                return Result<KeycloakUserInfo>.Success(new KeycloakUserInfo(
                    userInfo.Sub,
                    userInfo.Email,
                    userInfo.PreferredUsername ?? userInfo.Email,
                    userInfo.GivenName,
                    userInfo.FamilyName));
            }
        }

        var fallback = TryReadUserInfoFromJwt(accessToken);
        if (fallback is not null)
        {
            return Result<KeycloakUserInfo>.Success(fallback);
        }

        var details = await response.Content.ReadAsStringAsync(ct);
        return Result<KeycloakUserInfo>.Failure(new AppError(
            ErrorCodes.Unauthorized,
            $"Failed to read user info. Status: {(int)response.StatusCode}. {details}"));
    }

    private async Task<Result<string>> GetAdminTokenAsync(CancellationToken ct)
    {
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _options.AdminClientId,
            ["username"] = _options.AdminUsername,
            ["password"] = _options.AdminPassword
        };

        using var response = await _httpClient.PostAsync(AdminTokenRoute, new FormUrlEncodedContent(body), ct);
        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync(ct);
            return Result<string>.Failure(new AppError(ErrorCodes.Unauthorized, $"Failed to get admin token. {details}"));
        }

        var token = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken: ct);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            return Result<string>.Failure(new AppError(ErrorCodes.Unexpected, "Admin token response is invalid."));
        }

        return Result<string>.Success(token.AccessToken);
    }

    private static async Task<Result<AuthTokenDto>> ReadTokenResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync(ct);
            return Result<AuthTokenDto>.Failure(new AppError(ErrorCodes.Unauthorized, $"Authentication failed. {details}"));
        }

        var token = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken: ct);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken) || string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            return Result<AuthTokenDto>.Failure(new AppError(ErrorCodes.Unexpected, "Token response is invalid."));
        }

        return Result<AuthTokenDto>.Success(new AuthTokenDto(token.AccessToken, token.RefreshToken, token.ExpiresIn, token.TokenType));
    }

    private static KeycloakUserInfo? TryReadUserInfoFromJwt(string accessToken)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(accessToken))
        {
            return null;
        }

        var jwt = handler.ReadJwtToken(accessToken);
        var subject = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        var email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
        var preferredUsername = jwt.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value;
        var givenName = jwt.Claims.FirstOrDefault(c => c.Type == "given_name")?.Value;
        var familyName = jwt.Claims.FirstOrDefault(c => c.Type == "family_name")?.Value;

        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? $"{subject}@local.invalid" : email;
        var normalizedUsername = string.IsNullOrWhiteSpace(preferredUsername) ? normalizedEmail : preferredUsername;

        return new KeycloakUserInfo(subject, normalizedEmail, normalizedUsername, givenName, familyName);
    }

    private sealed record KeycloakTokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string RefreshToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn,
        [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string TokenType);

    private sealed record KeycloakUserInfoResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("sub")] string Sub,
        [property: System.Text.Json.Serialization.JsonPropertyName("email")] string Email,
        [property: System.Text.Json.Serialization.JsonPropertyName("preferred_username")] string? PreferredUsername,
        [property: System.Text.Json.Serialization.JsonPropertyName("given_name")] string? GivenName,
        [property: System.Text.Json.Serialization.JsonPropertyName("family_name")] string? FamilyName);
}
