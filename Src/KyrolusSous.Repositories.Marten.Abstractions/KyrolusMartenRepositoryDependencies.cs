using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

namespace KyrolusSous.Repositories.Marten.Abstractions;

public sealed record KyrolusMartenRepositoryDependencies(
    IKyrolusMartenObserver? Observer = null,
    IKyrolusMartenAuthorization? Authorization = null,
    IKyrolusMartenValidation? Validation = null,
    IKyrolusMartenSoftDeletePolicy? SoftDeletePolicy = null,
    IKyrolusMartenCacheProvider? CacheProvider = null,
    IKyrolusMartenResiliencePolicy? ResiliencePolicy = null,
    IKyrolusMartenTracing? Tracing = null);
