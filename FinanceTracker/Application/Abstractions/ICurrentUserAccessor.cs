namespace Application.Abstractions;

public interface ICurrentUserAccessor
{
    Guid UserId { get; }
    string KeycloakUserId { get; }
    string Username { get; }
    string Email { get; }
    bool IsAuthenticated { get; }
}
