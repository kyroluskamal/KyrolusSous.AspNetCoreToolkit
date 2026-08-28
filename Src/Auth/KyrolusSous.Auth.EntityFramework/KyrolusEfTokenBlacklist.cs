using KyrolusSous.Auth.TokenRevocation;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.Auth.EntityFramework;

public class KyrolusEfTokenBlacklist<TContext>(TContext context) : IKyrolusTokenBlacklist where TContext : DbContext
{
    private readonly TContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task RevokeTokenAsync(string jti, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);
        if (expiresAtUtc <= DateTimeOffset.UtcNow) return;

        var existing = await _context.Set<KyrolusEfRevokedTokenEntity>().FindAsync([jti.Trim()], cancellationToken);
        if (existing is null)
        {
            _context.Set<KyrolusEfRevokedTokenEntity>().Add(new KyrolusEfRevokedTokenEntity
            {
                Jti = jti.Trim(),
                ExpiresAtUtc = expiresAtUtc
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> IsTokenRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti)) return false;

        var entity = await _context.Set<KyrolusEfRevokedTokenEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Jti == jti.Trim(), cancellationToken);

        if (entity is null) return false;

        return DateTimeOffset.UtcNow < entity.ExpiresAtUtc;
    }

    public async Task RevokeUserTokensAsync(string userId, DateTimeOffset revokedBeforeUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var existing = await _context.Set<KyrolusEfUserRevocationEntity>().FindAsync([userId.Trim()], cancellationToken);
        if (existing is null)
        {
            _context.Set<KyrolusEfUserRevocationEntity>().Add(new KyrolusEfUserRevocationEntity
            {
                UserId = userId.Trim(),
                RevokedBeforeUtc = revokedBeforeUtc
            });
        }
        else if (revokedBeforeUtc > existing.RevokedBeforeUtc)
        {
            existing.RevokedBeforeUtc = revokedBeforeUtc;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsUserTokenRevokedAsync(string userId, DateTimeOffset tokenIssuedAtUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;

        var entity = await _context.Set<KyrolusEfUserRevocationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId.Trim(), cancellationToken);

        if (entity is null) return false;

        return tokenIssuedAtUtc.ToUnixTimeSeconds() <= entity.RevokedBeforeUtc.ToUnixTimeSeconds();
    }

    public async Task<int> PurgeExpiredRevocationsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expired = await _context.Set<KyrolusEfRevokedTokenEntity>()
            .Where(t => t.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);

        if (expired.Count > 0)
        {
            _context.Set<KyrolusEfRevokedTokenEntity>().RemoveRange(expired);
            return await _context.SaveChangesAsync(cancellationToken);
        }

        return 0;
    }
}
