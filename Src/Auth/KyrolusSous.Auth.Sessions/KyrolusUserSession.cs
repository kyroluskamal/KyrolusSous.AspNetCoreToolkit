namespace KyrolusSous.Auth.Sessions;

/// <summary>
/// Represents an active or historical authenticated user session on a specific client/device.
/// </summary>
public sealed class KyrolusUserSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceInfo { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActiveAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddDays(30);
    public bool IsRevoked { get; set; }

    public bool IsActive(DateTimeOffset? now = null, TimeSpan? clockSkew = null)
    {
        var current = now ?? DateTimeOffset.UtcNow;
        var skew = clockSkew ?? TimeSpan.Zero;
        return !IsRevoked && ExpiresAt.Add(skew) > current;
    }
}

public sealed class KyrolusSessionOptions
{
    /// <summary>
    /// Default lifetime of a user session. Defaults to 30 days.
    /// </summary>
    public TimeSpan DefaultSessionLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Optional limit on the number of active concurrent sessions per user.
    /// When exceeded, the oldest active session is automatically revoked.
    /// </summary>
    public int? MaxActiveSessionsPerUser { get; set; }
}
