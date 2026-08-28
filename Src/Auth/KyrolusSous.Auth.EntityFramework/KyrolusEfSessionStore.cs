using KyrolusSous.Auth.Sessions;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.Auth.EntityFramework;

public class KyrolusEfSessionStore<TContext>(TContext context) : IKyrolusSessionStore where TContext : DbContext
{
    private readonly TContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task CreateSessionAsync(KyrolusUserSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var entity = KyrolusEfSessionEntity.FromSession(session);
        _context.Set<KyrolusEfSessionEntity>().Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<KyrolusUserSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;

        var entity = await _context.Set<KyrolusEfSessionEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SessionId == sessionId.Trim(), cancellationToken);

        return entity?.ToSession();
    }

    public async Task<IReadOnlyList<KyrolusUserSession>> GetActiveUserSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return [];

        var now = DateTimeOffset.UtcNow;
        var entities = await _context.Set<KyrolusEfSessionEntity>()
            .AsNoTracking()
            .Where(s => s.UserId == userId.Trim() && !s.IsRevoked && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastActiveAt)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToSession()).ToList();
    }

    public async Task UpdateActivityAsync(string sessionId, DateTimeOffset lastActiveAt, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;

        var entity = await _context.Set<KyrolusEfSessionEntity>().FindAsync([sessionId.Trim()], cancellationToken);
        if (entity is not null)
        {
            entity.LastActiveAt = lastActiveAt;
            if (!string.IsNullOrEmpty(ipAddress))
            {
                entity.IpAddress = ipAddress;
            }
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;

        var entity = await _context.Set<KyrolusEfSessionEntity>().FindAsync([sessionId.Trim()], cancellationToken);
        if (entity is not null)
        {
            entity.IsRevoked = true;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeAllUserSessionsAsync(string userId, string? exceptSessionId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        var query = _context.Set<KyrolusEfSessionEntity>()
            .Where(s => s.UserId == userId.Trim() && !s.IsRevoked);

        if (!string.IsNullOrEmpty(exceptSessionId))
        {
            query = query.Where(s => s.SessionId != exceptSessionId.Trim());
        }

        var sessions = await query.ToListAsync(cancellationToken);
        foreach (var s in sessions)
        {
            s.IsRevoked = true;
        }

        if (sessions.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> PurgeInactiveSessionsAsync(DateTimeOffset? olderThanUtc = null, CancellationToken cancellationToken = default)
    {
        var cutoff = olderThanUtc ?? DateTimeOffset.UtcNow;
        var expired = await _context.Set<KyrolusEfSessionEntity>()
            .Where(s => s.ExpiresAt <= cutoff || (s.IsRevoked && s.LastActiveAt <= cutoff.AddDays(-7)))
            .ToListAsync(cancellationToken);

        if (expired.Count > 0)
        {
            _context.Set<KyrolusEfSessionEntity>().RemoveRange(expired);
            return await _context.SaveChangesAsync(cancellationToken);
        }

        return 0;
    }
}
