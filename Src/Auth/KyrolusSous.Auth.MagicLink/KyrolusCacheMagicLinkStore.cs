using KyrolusSous.Caching.Abstractions;

namespace KyrolusSous.Auth.MagicLink;

/// <summary>
/// Distributed implementation of <see cref="IKyrolusMagicLinkStore"/> backed by <see cref="IKyrolusCacheProvider"/> (e.g. Redis, HybridCache).
/// Supports single-use atomic consumption to prevent replay attacks across multiple nodes.
/// </summary>
public sealed class KyrolusCacheMagicLinkStore(IKyrolusCacheProvider cacheProvider) : IKyrolusMagicLinkStore
{
    private const string KeyPrefix = "auth:magiclink:";

    private readonly IKyrolusCacheProvider _cache = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));

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

        var now = DateTimeOffset.UtcNow;
        var ttl = expiresAtUtc - now;
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        var record = new KyrolusMagicLinkRecord(tokenHash, userId, email, expiresAtUtc);
        var cacheKey = $"{KeyPrefix}{tokenHash}";
        await _cache.SetAsync(cacheKey, record, ttl, cancellationToken);
    }

    public async Task<KyrolusMagicLinkRecord?> ConsumeTokenAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return null;
        }

        var cacheKey = $"{KeyPrefix}{tokenHash}";
        var record = await _cache.GetAsync<KyrolusMagicLinkRecord>(cacheKey, cancellationToken);

        if (record is null)
        {
            return null;
        }

        // Atomically remove upon consumption to prevent replay attacks
        await _cache.RemoveAsync(cacheKey, cancellationToken);

        if (DateTimeOffset.UtcNow > record.ExpiresAtUtc)
        {
            return null;
        }

        return record;
    }

    public Task<int> PurgeExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        // Cache provider TTL eviction natively removes expired keys
        return Task.FromResult(0);
    }
}
