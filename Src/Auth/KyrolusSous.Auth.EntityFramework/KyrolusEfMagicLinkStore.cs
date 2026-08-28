using KyrolusSous.Auth.MagicLink;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.Auth.EntityFramework;

public class KyrolusEfMagicLinkStore<TContext>(TContext context) : IKyrolusMagicLinkStore where TContext : DbContext
{
    private readonly TContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task SaveTokenAsync(
        string tokenHash,
        string userId,
        string email,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var entity = new KyrolusEfMagicLinkEntity
        {
            TokenHash = tokenHash.Trim(),
            UserId = userId.Trim(),
            Email = email.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = expiresAtUtc,
            IsConsumed = false
        };

        _context.Set<KyrolusEfMagicLinkEntity>().Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<KyrolusMagicLinkRecord?> ConsumeTokenAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash)) return null;

        var entity = await _context.Set<KyrolusEfMagicLinkEntity>()
            .FirstOrDefaultAsync(m => m.TokenHash == tokenHash.Trim() && !m.IsConsumed, cancellationToken);

        if (entity is null) return null;

        entity.IsConsumed = true;
        await _context.SaveChangesAsync(cancellationToken);

        if (DateTimeOffset.UtcNow > entity.ExpiresAtUtc)
        {
            return null;
        }

        return entity.ToRecord();
    }

    public async Task<int> PurgeExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expired = await _context.Set<KyrolusEfMagicLinkEntity>()
            .Where(m => m.ExpiresAtUtc <= now || m.IsConsumed)
            .ToListAsync(cancellationToken);

        if (expired.Count > 0)
        {
            _context.Set<KyrolusEfMagicLinkEntity>().RemoveRange(expired);
            return await _context.SaveChangesAsync(cancellationToken);
        }

        return 0;
    }
}
