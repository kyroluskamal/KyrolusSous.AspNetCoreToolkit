namespace KyrolusSous.Validation.Runtime;

public sealed class KyrolusValidationCacheKeyProvider : IKyrolusValidationCacheKeyProvider
{
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
