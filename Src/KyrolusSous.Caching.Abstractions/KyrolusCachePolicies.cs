using System.Collections.Concurrent;

namespace KyrolusSous.Caching.Abstractions;

public sealed record KyrolusCachePolicy(
    TimeSpan? AbsoluteExpirationRelativeToNow = null,
    TimeSpan? SlidingExpiration = null,
    TimeSpan? Jitter = null,
    TimeSpan? NegativeCacheTtl = null,
    bool? Enabled = null,
    string? KeySuffix = null,
    IReadOnlyCollection<string>? ExtraInvalidationKeys = null,
    IReadOnlyCollection<string>? ExtraInvalidationKeyPatterns = null);

public interface IKyrolusCachePolicyProvider
{
    KyrolusCachePolicy? GetPolicy(Type valueType, KyrolusCacheOperation operation);
}

public sealed class KyrolusNullCachePolicyProvider : IKyrolusCachePolicyProvider
{
    public static IKyrolusCachePolicyProvider Instance { get; } = new KyrolusNullCachePolicyProvider();

    public KyrolusCachePolicy? GetPolicy(Type valueType, KyrolusCacheOperation operation) => null;
}

public sealed class KyrolusCachePolicyRegistry : IKyrolusCachePolicyProvider
{
    private readonly ConcurrentDictionary<(Type, KyrolusCacheOperation), KyrolusCachePolicy> byTypeAndOperation = new();
    private readonly ConcurrentDictionary<KyrolusCacheOperation, KyrolusCachePolicy> byOperation = new();
    private KyrolusCachePolicy? defaultPolicy;

    public KyrolusCachePolicyRegistry SetDefault(KyrolusCachePolicy policy)
    {
        defaultPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public KyrolusCachePolicyRegistry SetForOperation(KyrolusCacheOperation operation, KyrolusCachePolicy policy)
    {
        byOperation[operation] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public KyrolusCachePolicyRegistry SetForType<T>(KyrolusCacheOperation operation, KyrolusCachePolicy policy)
    {
        byTypeAndOperation[(typeof(T), operation)] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public KyrolusCachePolicy? GetPolicy(Type valueType, KyrolusCacheOperation operation)
    {
        if (byTypeAndOperation.TryGetValue((valueType, operation), out var policy))
        {
            return policy;
        }

        if (byOperation.TryGetValue(operation, out policy))
        {
            return policy;
        }

        return defaultPolicy;
    }
}
