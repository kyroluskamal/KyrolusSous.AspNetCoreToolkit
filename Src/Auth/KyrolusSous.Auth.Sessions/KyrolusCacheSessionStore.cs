using KyrolusSous.Caching.Abstractions;

namespace KyrolusSous.Auth.Sessions;

/// <summary>
/// Distributed implementation of <see cref="IKyrolusSessionStore"/> backed by <see cref="IKyrolusCacheProvider"/> (e.g. Redis, HybridCache).
/// Supports multi-device tracking, remote logout, and cluster-wide synchronization.
/// </summary>
public sealed class KyrolusCacheSessionStore(IKyrolusCacheProvider cacheProvider) : IKyrolusSessionStore
{
    private const string SessionPrefix = "auth:session:id:";
    private const string UserSessionsPrefix = "auth:session:user:";

    private readonly IKyrolusCacheProvider _cache = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));

    public async Task CreateSessionAsync(KyrolusUserSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var ttl = session.ExpiresAt - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            ttl = TimeSpan.FromHours(24);
        }

        var sessionKey = $"{SessionPrefix}{session.SessionId}";
        await _cache.SetAsync(sessionKey, session, ttl, cancellationToken);

        // Track in user session list
        var userKey = $"{UserSessionsPrefix}{session.UserId}";
        var existingList = await _cache.GetAsync<List<string>>(userKey, cancellationToken) ?? [];
        if (!existingList.Contains(session.SessionId))
        {
            existingList.Add(session.SessionId);
            await _cache.SetAsync(userKey, existingList, TimeSpan.FromDays(30), cancellationToken);
        }
    }

    public async Task<KyrolusUserSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var sessionKey = $"{SessionPrefix}{sessionId}";
        return await _cache.GetAsync<KyrolusUserSession>(sessionKey, cancellationToken);
    }

    public async Task<IReadOnlyList<KyrolusUserSession>> GetActiveUserSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        var userKey = $"{UserSessionsPrefix}{userId}";
        var sessionIds = await _cache.GetAsync<List<string>>(userKey, cancellationToken);
        if (sessionIds is null || sessionIds.Count == 0)
        {
            return [];
        }

        var now = DateTimeOffset.UtcNow;
        var activeSessions = new List<KyrolusUserSession>();
        var validIds = new List<string>();

        foreach (var sid in sessionIds)
        {
            var session = await GetSessionAsync(sid, cancellationToken);
            if (session is not null && session.IsActive(now))
            {
                activeSessions.Add(session);
                validIds.Add(sid);
            }
        }

        if (validIds.Count != sessionIds.Count)
        {
            await _cache.SetAsync(userKey, validIds, TimeSpan.FromDays(30), cancellationToken);
        }

        return activeSessions.OrderByDescending(s => s.LastActiveAt).ToList();
    }

    public async Task UpdateActivityAsync(string sessionId, DateTimeOffset lastActiveAt, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;

        var session = await GetSessionAsync(sessionId, cancellationToken);
        if (session is not null)
        {
            session.LastActiveAt = lastActiveAt;
            if (!string.IsNullOrEmpty(ipAddress))
            {
                session.IpAddress = ipAddress;
            }

            var ttl = session.ExpiresAt - DateTimeOffset.UtcNow;
            if (ttl > TimeSpan.Zero)
            {
                var sessionKey = $"{SessionPrefix}{sessionId}";
                await _cache.SetAsync(sessionKey, session, ttl, cancellationToken);
            }
        }
    }

    public async Task RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;

        var session = await GetSessionAsync(sessionId, cancellationToken);
        if (session is not null)
        {
            session.IsRevoked = true;
            var sessionKey = $"{SessionPrefix}{sessionId}";
            await _cache.SetAsync(sessionKey, session, TimeSpan.FromDays(7), cancellationToken);
        }
    }

    public async Task RevokeAllUserSessionsAsync(string userId, string? exceptSessionId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        var userKey = $"{UserSessionsPrefix}{userId}";
        var sessionIds = await _cache.GetAsync<List<string>>(userKey, cancellationToken);
        if (sessionIds is null) return;

        foreach (var sid in sessionIds)
        {
            if (exceptSessionId is not null && string.Equals(sid, exceptSessionId, StringComparison.Ordinal))
            {
                continue;
            }

            await RevokeSessionAsync(sid, cancellationToken);
        }
    }

    public Task<int> PurgeInactiveSessionsAsync(DateTimeOffset? olderThanUtc = null, CancellationToken cancellationToken = default)
    {
        // Cache provider TTL natively purges expired entries
        return Task.FromResult(0);
    }
}
