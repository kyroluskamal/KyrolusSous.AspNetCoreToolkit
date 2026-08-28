namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Encapsulates the core service dependencies required by the <see cref="KyrolusRedisCacheProvider"/>, 
/// including serializer, key factory, options, diagnostic observer, and cache policies.
/// </summary>
/// <param name="serializer">The cache serialization engine.</param>
/// <param name="keyFactory">The key prefix and namespace factory.</param>
/// <param name="options">Redis cache options.</param>
/// <param name="observer">Optional diagnostic observer for metrics and logging.</param>
/// <param name="policyProvider">Optional entity and operation cache policy provider.</param>
public sealed class KyrolusRedisCacheDependencies(
    IKyrolusCacheSerializer serializer,
    IKyrolusCacheKeyFactory keyFactory,
    KyrolusRedisCacheOptions options,
    IKyrolusCacheObserver? observer = null,
    IKyrolusCachePolicyProvider? policyProvider = null)
{
    /// <summary>
    /// Gets a default instance configured with JSON serialization, default key factory, default options, and no-op observers.
    /// </summary>
    public static KyrolusRedisCacheDependencies Default { get; } =
        new KyrolusRedisCacheDependencies(
            new KyrolusJsonCacheSerializer(),
            new KyrolusCacheKeyFactory(),
            new KyrolusRedisCacheOptions(),
            KyrolusNullCacheObserver.Instance,
            KyrolusNullCachePolicyProvider.Instance);

    /// <summary>
    /// Gets the cache serializer.
    /// </summary>
    public IKyrolusCacheSerializer Serializer { get; } = serializer ?? throw new ArgumentNullException(nameof(serializer));

    /// <summary>
    /// Gets the key formatting factory.
    /// </summary>
    public IKyrolusCacheKeyFactory KeyFactory { get; } = keyFactory ?? throw new ArgumentNullException(nameof(keyFactory));

    /// <summary>
    /// Gets the Redis cache configuration options.
    /// </summary>
    public KyrolusRedisCacheOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Gets the cache diagnostic observer.
    /// </summary>
    public IKyrolusCacheObserver Observer { get; } = observer ?? KyrolusNullCacheObserver.Instance;

    /// <summary>
    /// Gets the entity cache policy provider.
    /// </summary>
    public IKyrolusCachePolicyProvider PolicyProvider { get; } = policyProvider ?? KyrolusNullCachePolicyProvider.Instance;
}
