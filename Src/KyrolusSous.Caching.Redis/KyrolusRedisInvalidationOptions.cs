namespace KyrolusSous.Caching.Redis;

public sealed class KyrolusRedisInvalidationOptions
{
    public string Channel { get; set; } = "kyrolus.cache.invalidation";
    public bool Publish { get; set; } = true;
    public bool Subscribe { get; set; } = true;

    internal static KyrolusRedisInvalidationOptions FromNearCacheOptions(KyrolusRedisNearCacheOptions options) =>
        new()
        {
            Channel = options.InvalidationChannel,
            Publish = options.PublishInvalidations,
            Subscribe = options.SubscribeInvalidations
        };
}
