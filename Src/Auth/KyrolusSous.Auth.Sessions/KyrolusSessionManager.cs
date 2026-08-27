using System.Collections.Concurrent;

namespace KyrolusSous.Auth.Sessions;

/// <summary>
/// Thread-safe in-memory session store for device sessions and revocation.
/// </summary>
public sealed class KyrolusInMemorySessionStore : IKyrolusSessionStore
{
    private readonly ConcurrentDictionary<string, KyrolusUserSession> _sessions = new(StringComparer.Ordinal);

    public Task CreateSessionAsync(KyrolusUserSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (_sessions.Count > 50_000)
        {
            _ = PurgeInactiveSessionsAsync(cancellationToken: cancellationToken);
        }

        _sessions[session.SessionId] = session;
        return Task.CompletedTask;
    }

    public Task<KyrolusUserSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<IReadOnlyList<KyrolusUserSession>> GetActiveUserSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var active = _sessions.Values
            .Where(s => string.Equals(s.UserId, userId, StringComparison.Ordinal) && s.IsActive(now))
            .OrderByDescending(s => s.LastActiveAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<KyrolusUserSession>>(active);
    }

    public Task UpdateActivityAsync(string sessionId, DateTimeOffset lastActiveAt, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.LastActiveAt = lastActiveAt;
            if (!string.IsNullOrEmpty(ipAddress))
            {
                session.IpAddress = ipAddress;
            }
        }

        return Task.CompletedTask;
    }

    public Task RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.IsRevoked = true;
        }

        return Task.CompletedTask;
    }

    public Task RevokeAllUserSessionsAsync(string userId, string? exceptSessionId = null, CancellationToken cancellationToken = default)
    {
        foreach (var session in _sessions.Values.ToArray())
        {
            if (string.Equals(session.UserId, userId, StringComparison.Ordinal))
            {
                if (exceptSessionId is null || !string.Equals(session.SessionId, exceptSessionId, StringComparison.Ordinal))
                {
                    session.IsRevoked = true;
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task<int> PurgeInactiveSessionsAsync(DateTimeOffset? olderThanUtc = null, CancellationToken cancellationToken = default)
    {
        var cutoff = olderThanUtc ?? DateTimeOffset.UtcNow;
        var purged = 0;
        foreach (var (k, v) in _sessions.ToArray())
        {
            if (v.IsRevoked || cutoff >= v.ExpiresAt)
            {
                if (_sessions.TryRemove(k, out _))
                {
                    purged++;
                }
            }
        }

        return Task.FromResult(purged);
    }
}

/// <summary>
/// High-level coordinator for session lifecycle, heartbeat activity, and device logouts.
/// </summary>
public sealed class KyrolusSessionManager : IKyrolusSessionManager
{
    private readonly IKyrolusSessionStore _store;
    private readonly KyrolusSessionOptions _options;

    public KyrolusSessionManager(IKyrolusSessionStore store, KyrolusSessionOptions? options = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? new KyrolusSessionOptions();
    }

    public async Task<KyrolusUserSession> StartSessionAsync(
        string userId,
        string? ipAddress = null,
        string? userAgent = null,
        string? deviceInfo = null,
        TimeSpan? customLifetime = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var lifetime = customLifetime ?? (_options.DefaultSessionLifetime < TimeSpan.FromMinutes(1)
            ? TimeSpan.FromMinutes(1)
            : _options.DefaultSessionLifetime);
        var now = DateTimeOffset.UtcNow;

        static string? SanitizeTelemetry(string? input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var truncated = input.Length > 512 ? input[..512] : input;
            return new string(truncated.Where(c => !char.IsControl(c) || c is '\t').ToArray());
        }

        var safeUserAgent = SanitizeTelemetry(userAgent);
        var safeDeviceInfo = SanitizeTelemetry(deviceInfo);

        var session = new KyrolusUserSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            IpAddress = ipAddress,
            UserAgent = safeUserAgent,
            DeviceInfo = safeDeviceInfo,
            CreatedAt = now,
            LastActiveAt = now,
            ExpiresAt = now.Add(lifetime),
            IsRevoked = false
        };

        if (_options.MaxActiveSessionsPerUser.HasValue && _options.MaxActiveSessionsPerUser.Value > 0)
        {
            var activeSessions = await _store.GetActiveUserSessionsAsync(userId, cancellationToken).ConfigureAwait(false);
            if (activeSessions.Count >= _options.MaxActiveSessionsPerUser.Value)
            {
                var toRevokeCount = activeSessions.Count - _options.MaxActiveSessionsPerUser.Value + 1;
                var oldestSessions = activeSessions.OrderBy(s => s.LastActiveAt).Take(toRevokeCount);
                foreach (var oldSession in oldestSessions)
                {
                    await _store.RevokeSessionAsync(oldSession.SessionId, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await _store.CreateSessionAsync(session, cancellationToken);
        return session;
    }

    public async Task<bool> ValidateSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        var session = await _store.GetSessionAsync(sessionId, cancellationToken);
        return session is not null && session.IsActive();
    }

    public Task<IReadOnlyList<KyrolusUserSession>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return _store.GetActiveUserSessionsAsync(userId, cancellationToken);
    }

    public Task RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return _store.RevokeSessionAsync(sessionId, cancellationToken);
    }

    public Task RevokeOtherSessionsAsync(string userId, string currentSessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSessionId);
        return _store.RevokeAllUserSessionsAsync(userId, currentSessionId, cancellationToken);
    }

    public async Task HeartbeatAsync(string sessionId, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var session = await _store.GetSessionAsync(sessionId, cancellationToken);
        if (session is null || !session.IsActive())
        {
            return;
        }
        await _store.UpdateActivityAsync(sessionId, DateTimeOffset.UtcNow, ipAddress, cancellationToken);
    }
}
