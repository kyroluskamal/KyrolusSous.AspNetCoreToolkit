using KyrolusSous.Auth.TokenRevocation;
using Marten;

namespace KyrolusSous.Auth.Marten;

public class KyrolusMartenRevokedToken
{
    public string Id { get; set; } = string.Empty; // Jti
    public DateTimeOffset ExpiresAtUtc { get; set; }
}

public class KyrolusMartenUserRevocation
{
    public string Id { get; set; } = string.Empty; // UserId
    public DateTimeOffset RevokedBeforeUtc { get; set; }
}

public class KyrolusMartenTokenBlacklist(IDocumentSession session) : IKyrolusTokenBlacklist
{
    private readonly IDocumentSession _session = session ?? throw new ArgumentNullException(nameof(session));

    public async Task RevokeTokenAsync(string jti, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);
        if (expiresAtUtc <= DateTimeOffset.UtcNow) return;

        _session.Store(new KyrolusMartenRevokedToken
        {
            Id = jti.Trim(),
            ExpiresAtUtc = expiresAtUtc
        });

        await _session.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsTokenRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti)) return false;

        var token = await _session.LoadAsync<KyrolusMartenRevokedToken>(jti.Trim(), cancellationToken);
        if (token is null) return false;

        return DateTimeOffset.UtcNow < token.ExpiresAtUtc;
    }

    public async Task RevokeUserTokensAsync(string userId, DateTimeOffset revokedBeforeUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var existing = await _session.LoadAsync<KyrolusMartenUserRevocation>(userId.Trim(), cancellationToken);
        if (existing is null || revokedBeforeUtc > existing.RevokedBeforeUtc)
        {
            _session.Store(new KyrolusMartenUserRevocation
            {
                Id = userId.Trim(),
                RevokedBeforeUtc = revokedBeforeUtc
            });
            await _session.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> IsUserTokenRevokedAsync(string userId, DateTimeOffset tokenIssuedAtUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;

        var userRev = await _session.LoadAsync<KyrolusMartenUserRevocation>(userId.Trim(), cancellationToken);
        if (userRev is null) return false;

        return tokenIssuedAtUtc.ToUnixTimeSeconds() <= userRev.RevokedBeforeUtc.ToUnixTimeSeconds();
    }

    public async Task<int> PurgeExpiredRevocationsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expired = await _session.Query<KyrolusMartenRevokedToken>()
            .Where(t => t.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);

        foreach (var t in expired)
        {
            _session.Delete(t);
        }

        if (expired.Count > 0)
        {
            await _session.SaveChangesAsync(cancellationToken);
        }

        return expired.Count;
    }
}
