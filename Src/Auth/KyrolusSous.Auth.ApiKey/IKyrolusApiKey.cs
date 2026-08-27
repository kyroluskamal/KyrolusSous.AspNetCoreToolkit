namespace KyrolusSous.Auth.ApiKey;

/// <summary>
/// Represents metadata and authorization details of an API Key.
/// </summary>
public interface IKyrolusApiKey
{
    string KeyHash { get; }
    string OwnerId { get; }
    string OwnerName { get; }
    IReadOnlyList<string> Scopes { get; }
    IReadOnlyList<string> Roles { get; }
    DateTimeOffset? ExpiresAtUtc { get; }
    bool IsActive { get; }
}

/// <summary>
/// Default in-memory or generic model for <see cref="IKyrolusApiKey"/>.
/// </summary>
public record KyrolusApiKey(
    string KeyHash,
    string OwnerId,
    string OwnerName,
    IReadOnlyList<string>? Scopes = null,
    IReadOnlyList<string>? Roles = null,
    DateTimeOffset? ExpiresAtUtc = null,
    bool IsActive = true) : IKyrolusApiKey
{
    public IReadOnlyList<string> Scopes { get; init; } = Scopes ?? [];
    public IReadOnlyList<string> Roles { get; init; } = Roles ?? [];
}
