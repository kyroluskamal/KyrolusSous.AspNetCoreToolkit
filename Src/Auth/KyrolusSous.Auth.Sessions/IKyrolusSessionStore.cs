namespace KyrolusSous.Auth.Sessions;

/// <summary>
/// Storage-agnostic persistence contract for managing user sessions.
/// Can be backed by Redis, EF Core, Marten, Distributed Cache, or Memory.
/// </summary>
public interface IKyrolusSessionStore
{
    Task CreateSessionAsync(KyrolusUserSession session, CancellationToken cancellationToken = default);

    Task<KyrolusUserSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KyrolusUserSession>> GetActiveUserSessionsAsync(string userId, CancellationToken cancellationToken = default);

    Task UpdateActivityAsync(string sessionId, DateTimeOffset lastActiveAt, string? ipAddress = null, CancellationToken cancellationToken = default);

    Task RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task RevokeAllUserSessionsAsync(string userId, string? exceptSessionId = null, CancellationToken cancellationToken = default);

    Task<int> PurgeInactiveSessionsAsync(DateTimeOffset? olderThanUtc = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service contract for orchestrating device sessions, activity heartbeats, and remote logouts.
/// </summary>
public interface IKyrolusSessionManager
{
    Task<KyrolusUserSession> StartSessionAsync(
        string userId,
        string? ipAddress = null,
        string? userAgent = null,
        string? deviceInfo = null,
        TimeSpan? customLifetime = null,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KyrolusUserSession>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken = default);

    Task RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    Task RevokeOtherSessionsAsync(string userId, string currentSessionId, CancellationToken cancellationToken = default);

    Task HeartbeatAsync(string sessionId, string? ipAddress = null, CancellationToken cancellationToken = default);
}
