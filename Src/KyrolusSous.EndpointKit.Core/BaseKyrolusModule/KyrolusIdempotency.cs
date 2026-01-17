using KyrolusSous.Caching.Abstractions;
using System.Collections.Concurrent;

namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

public sealed record KyrolusIdempotencyEntry(object? Value, int StatusCode, string? ContentType);

public interface IKyrolusIdempotencyStore
{
    Task<KyrolusIdempotencyEntry?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, KyrolusIdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken = default);
}

public sealed class KyrolusInMemoryIdempotencyStore : IKyrolusIdempotencyStore
{
    private readonly ConcurrentDictionary<string, (KyrolusIdempotencyEntry Entry, DateTimeOffset ExpiresAt)> store = new();

    public Task<KyrolusIdempotencyEntry?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!store.TryGetValue(key, out var entry))
        {
            return Task.FromResult<KyrolusIdempotencyEntry?>(null);
        }

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            store.TryRemove(key, out _);
            return Task.FromResult<KyrolusIdempotencyEntry?>(null);
        }

        return Task.FromResult<KyrolusIdempotencyEntry?>(entry.Entry);
    }

    public Task SetAsync(string key, KyrolusIdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(ttl);
        store[key] = (entry, expiresAt);
        return Task.CompletedTask;
    }
}

public sealed class KyrolusCacheIdempotencyStore : IKyrolusIdempotencyStore
{
    private const string KeyPrefix = "idempotency";
    private readonly ICacheProvider cache;
    private readonly ICacheKeyContext? cacheKeyContext;
    private readonly KyrolusInMemoryIdempotencyStore fallback = new();

    public KyrolusCacheIdempotencyStore(ICacheProvider cache, ICacheKeyContext? cacheKeyContext = null)
    {
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.cacheKeyContext = cacheKeyContext;
    }

    public Task<KyrolusIdempotencyEntry?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (cache is NullCacheProvider)
        {
            return fallback.GetAsync(key, cancellationToken);
        }

        var cacheKey = BuildKey(key);
        return cache.GetAsync<KyrolusIdempotencyEntry>(cacheKey, cancellationToken);
    }

    public Task SetAsync(string key, KyrolusIdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (cache is NullCacheProvider)
        {
            return fallback.SetAsync(key, entry, ttl, cancellationToken);
        }

        var options = new KyrolusCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Region = ResolveRegion(),
            TenantId = cacheKeyContext?.TenantId
        };
        var cacheKey = BuildKey(key);
        return cache.SetAsync(cacheKey, entry, options, cancellationToken);
    }

    private string BuildKey(string key)
    {
        var scope = cacheKeyContext?.ScopeKey;
        if (!string.IsNullOrWhiteSpace(scope))
        {
            return $"{KeyPrefix}:scope={Uri.EscapeDataString(scope)}:{key}";
        }

        if (!string.IsNullOrWhiteSpace(cacheKeyContext?.TenantId))
        {
            return $"{KeyPrefix}:tenant={Uri.EscapeDataString(cacheKeyContext.TenantId)}:{key}";
        }

        return $"{KeyPrefix}:{key}";
    }

    private string? ResolveRegion()
    {
        if (!string.IsNullOrWhiteSpace(cacheKeyContext?.Region)) return cacheKeyContext.Region;
        if (!string.IsNullOrWhiteSpace(cacheKeyContext?.ScopeKey)) return cacheKeyContext.ScopeKey;
        return cacheKeyContext?.TenantId;
    }
}
