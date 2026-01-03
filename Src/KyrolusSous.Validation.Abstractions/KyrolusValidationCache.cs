namespace KyrolusSous.Validation.Abstractions;

public enum KyrolusValidationCacheMode
{
    None = 0,
    SuccessOnly = 1,
    FailuresOnly = 2,
    All = 3
}

public sealed record KyrolusValidationCacheEntry(
    string Key,
    KyrolusValidationCacheMode Mode,
    TimeSpan Ttl);

public interface IKyrolusValidationCacheable
{
    string? CacheKey { get; }
    TimeSpan? CacheTtl { get; }
    KyrolusValidationCacheMode CacheMode { get; }
}

public interface IKyrolusValidationNegativeCacheable
{
    TimeSpan? NegativeCacheTtl { get; }
}

public interface IKyrolusValidationCacheKeyProvider
{
    KyrolusValidationCacheEntry? GetCacheEntry(object request, KyrolusValidationContext context);
}

public interface IKyrolusValidationCacheStore
{
    bool TryGet(string key, out IReadOnlyList<KyrolusValidationFailure> failures);
    void Set(string key, IReadOnlyList<KyrolusValidationFailure> failures, TimeSpan ttl);
}

public static class KyrolusValidationCacheDefaults
{
    public static TimeSpan DefaultTtl { get; } = TimeSpan.FromMinutes(5);
    public static TimeSpan NegativeTtl { get; } = TimeSpan.FromSeconds(30);
    public static KyrolusValidationCacheMode DefaultMode { get; } = KyrolusValidationCacheMode.All;
}
