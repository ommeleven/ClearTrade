using Brokerage.Core.Interfaces;

namespace Brokerage.Core.Models;

public static class Roles
{
    public const string Client = "Client";
    public const string Admin = "Admin";
}

public class User : IEntity
{
    /// <summary>The normalized (lower-case) username.</summary>
    public string Id { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Client;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public record AuthToken(string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>The authenticated caller, as seen by the service layer.</summary>
public record Caller(string UserId, string Role)
{
    public bool IsAdmin => Role == Roles.Admin;
}
