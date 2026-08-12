namespace KyrolusSous.Validation.Runtime;

public sealed class KyrolusValidationCacheKeyProvider : IKyrolusValidationCacheKeyProvider
{
    public KyrolusValidationCacheEntry? GetCacheEntry(object request, KyrolusValidationContext context)
    {
        if (request is not IKyrolusValidationCacheable cacheable) return null;

        if (string.IsNullOrWhiteSpace(cacheable.CacheKey)) return null;

        var mode = cacheable.CacheMode == KyrolusValidationCacheMode.None
            ? KyrolusValidationCacheDefaults.DefaultMode
            : cacheable.CacheMode;

        if (mode == KyrolusValidationCacheMode.None) return null;

        var ttl = cacheable.CacheTtl ?? KyrolusValidationCacheDefaults.DefaultTtl;

        if (ttl <= TimeSpan.Zero) return null;

        return new KyrolusValidationCacheEntry(cacheable.CacheKey, mode, ttl);
    }
}
