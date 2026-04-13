using Domain.Common;

namespace Domain.Users;

public sealed class User : Entity
{
    public string KeycloakUserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public string? AvatarUrl { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public SubscriptionType CurrentSubscriptionType { get; set; } = SubscriptionType.Free;
    public bool HasActivePremium { get; set; }
    public DateTime? SubscriptionExpiresAt { get; set; }
}
