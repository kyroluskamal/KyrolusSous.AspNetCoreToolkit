using KyrolusSous.Auth.Sessions;
using Marten;

namespace KyrolusSous.Auth.Marten;

public class KyrolusMartenSessionDocument
{
    public string Id { get; set; } = string.Empty; // SessionId
    public string UserId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceInfo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastActiveAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }

    public KyrolusUserSession ToSession()
    {
        return new KyrolusUserSession
        {
            SessionId = Id,
            UserId = UserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent,
            DeviceInfo = DeviceInfo,
            CreatedAt = CreatedAt,
            LastActiveAt = LastActiveAt,
            ExpiresAt = ExpiresAt,
            IsRevoked = IsRevoked
        };
    }

    public static KyrolusMartenSessionDocument FromSession(KyrolusUserSession s)
    {
        return new KyrolusMartenSessionDocument
        {
            Id = s.SessionId,
            UserId = s.UserId,
            IpAddress = s.IpAddress,
            UserAgent = s.UserAgent,
            DeviceInfo = s.DeviceInfo,
            CreatedAt = s.CreatedAt,
            LastActiveAt = s.LastActiveAt,
            ExpiresAt = s.ExpiresAt,
            IsRevoked = s.IsRevoked
        };
    }
}

public class KyrolusMartenSessionStore(IDocumentSession session) : IKyrolusSessionStore
{
    private readonly IDocumentSession _session = session ?? throw new ArgumentNullException(nameof(session));

    public async Task CreateSessionAsync(KyrolusUserSession userSession, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userSession);

        var doc = KyrolusMartenSessionDocument.FromSession(userSession);
        _session.Store(doc);
        await _session.SaveChangesAsync(cancellationToken);
    }

    public async Task<KyrolusUserSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;

        var doc = await _session.LoadAsync<KyrolusMartenSessionDocument>(sessionId.Trim(), cancellationToken);
        return doc?.ToSession();
    }

    public async Task<IReadOnlyList<KyrolusUserSession>> GetActiveUserSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return [];

        var now = DateTimeOffset.UtcNow;
        var docs = await _session.Query<KyrolusMartenSessionDocument>()
            .Where(s => s.UserId == userId.Trim() && !s.IsRevoked && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastActiveAt)
            .ToListAsync(cancellationToken);

        return docs.Select(d => d.ToSession()).ToList();
    }

    public async Task UpdateActivityAsync(string sessionId, DateTimeOffset lastActiveAt, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;

        var doc = await _session.LoadAsync<KyrolusMartenSessionDocument>(sessionId.Trim(), cancellationToken);
        if (doc is not null)
        {
            doc.LastActiveAt = lastActiveAt;
            if (!string.IsNullOrEmpty(ipAddress))
            {
                doc.IpAddress = ipAddress;
            }
            _session.Store(doc);
            await _session.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;

        var doc = await _session.LoadAsync<KyrolusMartenSessionDocument>(sessionId.Trim(), cancellationToken);
        if (doc is not null)
        {
            doc.IsRevoked = true;
            _session.Store(doc);
            await _session.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeAllUserSessionsAsync(string userId, string? exceptSessionId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        var query = _session.Query<KyrolusMartenSessionDocument>()
            .Where(s => s.UserId == userId.Trim() && !s.IsRevoked);

        if (!string.IsNullOrEmpty(exceptSessionId))
        {
            query = query.Where(s => s.Id != exceptSessionId.Trim());
        }

        var docs = await query.ToListAsync(cancellationToken);
        foreach (var d in docs)
        {
            d.IsRevoked = true;
            _session.Store(d);
        }

        if (docs.Count > 0)
        {
            await _session.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> PurgeInactiveSessionsAsync(DateTimeOffset? olderThanUtc = null, CancellationToken cancellationToken = default)
    {
        var cutoff = olderThanUtc ?? DateTimeOffset.UtcNow;
        var expired = await _session.Query<KyrolusMartenSessionDocument>()
            .Where(s => s.ExpiresAt <= cutoff || (s.IsRevoked && s.LastActiveAt <= cutoff.AddDays(-7)))
            .ToListAsync(cancellationToken);

        foreach (var d in expired)
        {
            _session.Delete(d);
        }

        if (expired.Count > 0)
        {
            await _session.SaveChangesAsync(cancellationToken);
        }

        return expired.Count;
    }
}
