namespace Identity.Models;

public sealed class KeycloakOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8080";
    public string Realm { get; set; } = "financetracker";

    public string ClientId { get; set; } = "financetracker-web";
    public string? ClientSecret { get; set; }

    public string AdminClientId { get; set; } = "admin-cli";
    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = "admin";

    public bool RequireHttpsMetadata { get; set; }

    public string Authority => $"{BaseUrl.TrimEnd('/')}/realms/{Realm}";
    public string Audience { get; set; } = "financetracker-api";
    public string[] ValidAudiences { get; set; } = ["financetracker-api", "account", "financetracker-web"];
}
