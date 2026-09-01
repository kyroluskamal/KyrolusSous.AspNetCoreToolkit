namespace KyrolusSous.Validation.Runtime;

/// <summary>
/// Default <see cref="IKyrolusValidationCacheKeyProvider"/>: opts a request into caching only when it implements
/// <see cref="IKyrolusValidationCacheable"/> with a non-blank <see cref="IKyrolusValidationCacheable.CacheKey"/>,
/// a <see cref="IKyrolusValidationCacheable.CacheMode"/> other than <see cref="KyrolusValidationCacheMode.None"/>,
/// and a positive TTL. Registered automatically by <see cref="ServiceCollectionExtensions.AddKyrolusValidationRuntime"/>.
/// </summary>
public sealed class KyrolusValidationCacheKeyProvider : IKyrolusValidationCacheKeyProvider
{
    /// <inheritdoc />
    public KyrolusValidationCacheEntry? GetCacheEntry(object request, KyrolusValidationContext context)
    {
        if (request is not IKyrolusValidationCacheable cacheable
            || string.IsNullOrWhiteSpace(cacheable.CacheKey)
            || cacheable.CacheMode == KyrolusValidationCacheMode.None
            || cacheable.CacheTtl <= TimeSpan.Zero)
            return null;

        var ttl = cacheable.CacheTtl ?? KyrolusValidationCacheDefaults.DefaultTtl;
        return new KyrolusValidationCacheEntry(cacheable.CacheKey, cacheable.CacheMode, ttl);
    }
}
