namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Specifies the caching behavior mode for validation results.
/// </summary>
public enum KyrolusValidationCacheMode
{
    /// <summary>Do not cache any validation results.</summary>
    None = 0,
    /// <summary>Cache only successful validation results (0 failures).</summary>
    SuccessOnly = 1,
    /// <summary>Cache only failed validation results (failures &gt; 0).</summary>
    FailuresOnly = 2,
    /// <summary>Cache all validation outcomes regardless of success or failure.</summary>
    All = 3
}

/// <summary>
/// Represents a cached validation outcome entry with key, mode, and time-to-live duration.
/// </summary>
/// <param name="Key">The unique cache key.</param>
/// <param name="Mode">The caching condition mode.</param>
/// <param name="Ttl">The lifespan of this cache entry.</param>
public sealed record KyrolusValidationCacheEntry(
    string Key,
    KyrolusValidationCacheMode Mode,
    TimeSpan Ttl);

/// <summary>
/// Marks a request as eligible for validation result caching.
/// </summary>
/// <example>
/// <code>
/// public class LookupCountryRequest : IKyrolusValidationCacheable
/// {
///     public string CountryCode { get; set; } = string.Empty;
/// 
///     public string? CacheKey => $"val:country:{CountryCode}";
///     public TimeSpan? CacheTtl => TimeSpan.FromHours(1);
///     public KyrolusValidationCacheMode CacheMode => KyrolusValidationCacheMode.All;
/// }
/// </code>
/// </example>
public interface IKyrolusValidationCacheable
{
    /// <summary>Gets the unique cache key for this request.</summary>
    string? CacheKey { get; }

    /// <summary>Gets the optional TTL duration override.</summary>
    TimeSpan? CacheTtl { get; }

    /// <summary>Gets the cache mode determining whether successes or failures should be stored.</summary>
    KyrolusValidationCacheMode CacheMode { get; }
}

/// <summary>
/// Configures negative caching (caching failures with a shorter lifespan).
/// </summary>
public interface IKyrolusValidationNegativeCacheable
{
    /// <summary>Gets the TTL duration for caching failed validation outcomes.</summary>
    TimeSpan? NegativeCacheTtl { get; }
}

/// <summary>
/// Defines a provider for resolving cache keys dynamically from requests.
/// </summary>
public interface IKyrolusValidationCacheKeyProvider
{
    /// <summary>Resolves a cache entry for the given request and context.</summary>
    KyrolusValidationCacheEntry? GetCacheEntry(object request, KyrolusValidationContext context);
}

/// <summary>
/// Defines the storage mechanism for cached validation failures (e.g., MemoryCache, Redis).
/// </summary>
public interface IKyrolusValidationCacheStore
{
    /// <summary>Attempts to retrieve cached validation failures by key.</summary>
    bool TryGet(string key, out IReadOnlyList<KyrolusValidationFailure> failures);

    /// <summary>Stores validation failures in the cache store with specified TTL.</summary>
    void Set(string key, IReadOnlyList<KyrolusValidationFailure> failures, TimeSpan ttl);
}

/// <summary>
/// Default configuration values for validation caching.
/// </summary>
public static class KyrolusValidationCacheDefaults
{
    /// <summary>Default positive cache lifespan (5 minutes).</summary>
    public static TimeSpan DefaultTtl { get; } = TimeSpan.FromMinutes(5);

    /// <summary>Default negative cache lifespan for failures (30 seconds).</summary>
    public static TimeSpan NegativeTtl { get; } = TimeSpan.FromSeconds(30);

    /// <summary>Default caching mode (<see cref="KyrolusValidationCacheMode.All"/>).</summary>
    public static KyrolusValidationCacheMode DefaultMode { get; } = KyrolusValidationCacheMode.All;
}
