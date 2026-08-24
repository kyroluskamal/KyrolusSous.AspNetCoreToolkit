namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Configures Redis Pub/Sub broadcast channel and subscription settings for cluster-wide cache invalidations.
/// </summary>
public sealed class KyrolusRedisInvalidationOptions
{
    /// <summary>
    /// Gets or sets the Redis Pub/Sub channel name used to broadcast and receive eviction messages. 
    /// Defaults to <c>"kyrolus.cache.invalidation"</c>.
    /// </summary>
    public string Channel { get; set; } = "kyrolus.cache.invalidation";

    /// <summary>
    /// Gets or sets whether this node should publish invalidation messages to the Redis channel when it mutates data. Defaults to <c>true</c>.
    /// </summary>
    public bool Publish { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this node should subscribe to the Redis channel to receive eviction events from other nodes. Defaults to <c>true</c>.
    /// </summary>
    public bool Subscribe { get; set; } = true;

    internal static KyrolusRedisInvalidationOptions FromNearCacheOptions(KyrolusRedisNearCacheOptions options) =>
        new()
        {
            Channel = options.InvalidationChannel,
            Publish = options.PublishInvalidations,
            Subscribe = options.SubscribeInvalidations
        };
}
