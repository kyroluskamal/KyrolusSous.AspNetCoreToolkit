namespace KyrolusSous.Caching.Redis;

public sealed class KyrolusRedisNearCacheOptions
{
    public string InvalidationChannel { get; set; } = "kyrolus.cache.invalidation";
    public TimeSpan? DefaultL1Ttl { get; set; }
    public TimeSpan? DefaultL1SlidingTtl { get; set; }
    public TimeSpan? L1Jitter { get; set; }
    public bool PublishInvalidations { get; set; } = true;
    public bool SubscribeInvalidations { get; set; } = true;
}
