namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Configures settings for the Near-Cache (L1 In-Memory + L2 Distributed Redis) hybrid caching architecture.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is Near-Cache?</b>
/// Near-Cache combines the blazing speed of local in-memory caching with the consistency of distributed Redis:
/// <list type="bullet">
///   <item><description><b>L1 (Local In-Memory Cache):</b> Serves hot data in nanoseconds (0.00005 ms) directly from the application's RAM with <b>zero network roundtrips</b>.</description></item>
///   <item><description><b>L2 (Distributed Redis):</b> Shared by all servers to store all cached data across the cluster.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Real-World Use Case:</b>
/// High-traffic homepages or product catalogs serving 100,000 requests per minute. 
/// Serving from L1 memory eliminates network socket bottlenecks on the Redis instance entirely.
/// When any server updates a product, it broadcasts an eviction event across Redis Pub/Sub to instantly evict 
/// stale data from all other servers' L1 memory.
/// </para>
/// </remarks>
public sealed class KyrolusRedisNearCacheOptions
{
    /// <summary>
    /// Gets or sets the Redis Pub/Sub channel name used to broadcast and receive L1 cache eviction messages across servers. 
    /// Defaults to <c>"kyrolus.cache.invalidation"</c>.
    /// </summary>
    public string InvalidationChannel { get; set; } = "kyrolus.cache.invalidation";

    /// <summary>
    /// Gets or sets the maximum absolute TTL for items stored in the local L1 memory cache.
    /// If <c>null</c>, uses the L2 cache entry duration.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// Setting L1 TTL to 1 minute (<c>TimeSpan.FromMinutes(1)</c>) ensures local RAM is refreshed frequently 
    /// even if network pub/sub packets were somehow delayed.
    /// </remarks>
    public TimeSpan? DefaultL1Ttl { get; set; }

    /// <summary>
    /// Gets or sets the sliding expiration TTL for items stored in local L1 memory.
    /// </summary>
    public TimeSpan? DefaultL1SlidingTtl { get; set; }

    /// <summary>
    /// Gets or sets random jitter variance applied to L1 memory expiration to prevent simultaneous L1 cache misses.
    /// </summary>
    public TimeSpan? L1Jitter { get; set; }

    /// <summary>
    /// Gets or sets whether this server should broadcast invalidation messages to other cluster nodes when it modifies cache data. Defaults to <c>true</c>.
    /// </summary>
    public bool PublishInvalidations { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this server should listen for invalidation messages from other nodes to evict local L1 items. Defaults to <c>true</c>.
    /// </summary>
    public bool SubscribeInvalidations { get; set; } = true;

    /// <summary>
    /// Sets the invalidation channel name fluently.
    /// </summary>
    public KyrolusRedisNearCacheOptions WithInvalidationChannel(string channel)
    {
        InvalidationChannel = channel;
        return this;
    }

    /// <summary>
    /// Sets the default L1 TTL fluently.
    /// </summary>
    public KyrolusRedisNearCacheOptions WithDefaultL1Ttl(TimeSpan ttl)
    {
        DefaultL1Ttl = ttl;
        return this;
    }

    /// <summary>
    /// Sets the default L1 sliding TTL fluently.
    /// </summary>
    public KyrolusRedisNearCacheOptions WithDefaultL1SlidingTtl(TimeSpan slidingTtl)
    {
        DefaultL1SlidingTtl = slidingTtl;
        return this;
    }

    /// <summary>
    /// Sets the L1 jitter variance fluently.
    /// </summary>
    public KyrolusRedisNearCacheOptions WithL1Jitter(TimeSpan jitter)
    {
        L1Jitter = jitter;
        return this;
    }
}
