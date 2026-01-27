using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

namespace KyrolusSous.Repositories.Marten.Abstractions;

public sealed record KyrolusMartenRepositoryDependencies(
    IKyrolusMartenObserver? Observer = null,
    IKyrolusMartenAuthorization? Authorization = null,
    IKyrolusMartenValidation? Validation = null,
    IKyrolusMartenSoftDeletePolicy? SoftDeletePolicy = null,
    ICacheProvider? CacheProvider = null,
    ICacheKeyContext? CacheKeyContext = null,
    IKyrolusRepositoryCachePolicyProvider? CachePolicyProvider = null,
    KyrolusCachePolicy? CachePolicy = null,
    IKyrolusMartenRepositoryPolicyProvider? PolicyProvider = null,
    IKyrolusMartenResiliencePolicy? ResiliencePolicy = null,
    IKyrolusMartenTracing? Tracing = null);
