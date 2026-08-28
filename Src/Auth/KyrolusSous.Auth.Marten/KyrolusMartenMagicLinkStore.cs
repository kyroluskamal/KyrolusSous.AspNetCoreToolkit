using KyrolusSous.Auth.MagicLink;
using Marten;

namespace KyrolusSous.Auth.Marten;

public class KyrolusMartenMagicLinkDocument
{
    public string Id { get; set; } = string.Empty; // TokenHash
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool IsConsumed { get; set; }

    public KyrolusMagicLinkRecord ToRecord()
    {
        return new KyrolusMagicLinkRecord(Id, UserId, Email, ExpiresAtUtc);
    }
}

public class KyrolusMartenMagicLinkStore(IDocumentSession session) : IKyrolusMagicLinkStore
{
    private readonly IDocumentSession _session = session ?? throw new ArgumentNullException(nameof(session));

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

        var doc = new KyrolusMartenMagicLinkDocument
        {
            Id = tokenHash.Trim(),
            UserId = userId.Trim(),
            Email = email.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = expiresAtUtc,
            IsConsumed = false
        };

        _session.Store(doc);
        await _session.SaveChangesAsync(cancellationToken);
    }

    public async Task<KyrolusMagicLinkRecord?> ConsumeTokenAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash)) return null;

        var doc = await _session.LoadAsync<KyrolusMartenMagicLinkDocument>(tokenHash.Trim(), cancellationToken);
        if (doc is null || doc.IsConsumed) return null;

        doc.IsConsumed = true;
        _session.Store(doc);
        await _session.SaveChangesAsync(cancellationToken);

        if (DateTimeOffset.UtcNow > doc.ExpiresAtUtc)
        {
            return null;
        }

        return doc.ToRecord();
    }

    public async Task<int> PurgeExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expired = await _session.Query<KyrolusMartenMagicLinkDocument>()
            .Where(m => m.ExpiresAtUtc <= now || m.IsConsumed)
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
