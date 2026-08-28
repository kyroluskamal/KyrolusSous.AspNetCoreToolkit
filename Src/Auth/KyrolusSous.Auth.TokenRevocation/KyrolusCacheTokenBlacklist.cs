using KyrolusSous.Caching.Abstractions;

namespace KyrolusSous.Auth.TokenRevocation;

/// <summary>
/// Distributed implementation of <see cref="IKyrolusTokenBlacklist"/> using <see cref="IKyrolusCacheProvider"/> (e.g. Redis, HybridCache).
/// Automatically synchronizes revoked JTIs and user revocation stamps across multi-node Kubernetes clusters.
/// </summary>
public sealed class KyrolusCacheTokenBlacklist(IKyrolusCacheProvider cacheProvider) : IKyrolusTokenBlacklist
{
    private const string JtiPrefix = "auth:revoked:jti:";
    private const string UserPrefix = "auth:revoked:user:";

    private readonly IKyrolusCacheProvider _cache = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));

    public async Task RevokeTokenAsync(string jti, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);
        var now = DateTimeOffset.UtcNow;
        if (expiresAtUtc <= now)
        {
            return;
        }

        var ttl = expiresAtUtc - now;
        var key = $"{JtiPrefix}{jti.Trim()}";
        await _cache.SetAsync(key, expiresAtUtc.ToUnixTimeSeconds(), ttl, cancellationToken);
    }

    public async Task<bool> IsTokenRevokedAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return false;
        }

        var key = $"{JtiPrefix}{jti.Trim()}";
        var expiresUnixSeconds = await _cache.GetAsync<long?>(key, cancellationToken);
        if (!expiresUnixSeconds.HasValue)
        {
            return false;
        }

        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() < expiresUnixSeconds.Value;
    }

    public async Task RevokeUserTokensAsync(string userId, DateTimeOffset revokedBeforeUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var key = $"{UserPrefix}{userId.Trim()}";

        var current = await _cache.GetAsync<long?>(key, cancellationToken);
        var newUnixSeconds = revokedBeforeUtc.ToUnixTimeSeconds();

        if (!current.HasValue || newUnixSeconds > current.Value)
        {
            // Retain user revocation marker for 30 days by default
            await _cache.SetAsync(key, newUnixSeconds, TimeSpan.FromDays(30), cancellationToken);
        }
    }

    public async Task<bool> IsUserTokenRevokedAsync(string userId, DateTimeOffset tokenIssuedAtUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var key = $"{UserPrefix}{userId.Trim()}";
        var revokedBefore = await _cache.GetAsync<long?>(key, cancellationToken);
        if (!revokedBefore.HasValue)
        {
            return false;
        }

        return tokenIssuedAtUtc.ToUnixTimeSeconds() <= revokedBefore.Value;
    }

    public Task<int> PurgeExpiredRevocationsAsync(CancellationToken cancellationToken = default)
    {
        // Cache providers handle TTL eviction automatically natively
        return Task.FromResult(0);
    }
}
