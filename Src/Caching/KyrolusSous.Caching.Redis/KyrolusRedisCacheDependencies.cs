using KyrolusSous.Caching.Abstractions;

namespace KyrolusSous.Caching.Redis;

public sealed class KyrolusRedisCacheDependencies(
    IKyrolusCacheSerializer serializer,
    IKyrolusCacheKeyFactory keyFactory,
    KyrolusRedisCacheOptions options,
    IKyrolusCacheObserver? observer = null,
    IKyrolusCachePolicyProvider? policyProvider = null)
{
    public static KyrolusRedisCacheDependencies Default { get; } =
        new KyrolusRedisCacheDependencies(
            new KyrolusJsonCacheSerializer(),
            new KyrolusCacheKeyFactory(),
            new KyrolusRedisCacheOptions(),
            KyrolusNullCacheObserver.Instance,
            KyrolusNullCachePolicyProvider.Instance);

    public IKyrolusCacheSerializer Serializer { get; } = serializer ?? throw new ArgumentNullException(nameof(serializer));
    public IKyrolusCacheKeyFactory KeyFactory { get; } = keyFactory ?? throw new ArgumentNullException(nameof(keyFactory));
    public KyrolusRedisCacheOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));
    public IKyrolusCacheObserver Observer { get; } = observer ?? KyrolusNullCacheObserver.Instance;
    public IKyrolusCachePolicyProvider PolicyProvider { get; } = policyProvider ?? KyrolusNullCachePolicyProvider.Instance;
}
