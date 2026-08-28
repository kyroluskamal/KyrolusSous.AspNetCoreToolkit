using KyrolusSous.Repositories.Marten.Runtime.Repository.Decorators;

namespace KyrolusSous.Repositories.Marten.Runtime.Repository;

public static class KyrolusMartenRepositoryFactory
{
    public static IKyrolusMartenRepositoryAsync<TSession, TEntity, TKey> Create<TSession, TEntity, TKey>(
        IServiceProvider services,
        TSession session,
        KyrolusMartenRepositoryDependencies? deps = null,
        bool useDecorator = true)
        where TSession : IDocumentSession
        where TEntity : class
        where TKey : IEquatable<TKey>
    {
        var effectiveDeps = deps ?? new KyrolusMartenRepositoryDependencies(
            Observer: services.GetService<IKyrolusMartenObserver>(),
            Authorization: services.GetService<IKyrolusMartenAuthorization>(),
            Validation: services.GetService<IKyrolusMartenValidation>(),
            SoftDeletePolicy: services.GetService<IKyrolusMartenSoftDeletePolicy>(),
            CacheProvider: services.GetService<IKyrolusCacheProvider>(),
            CacheKeyContext: services.GetService<IKyrolusCacheKeyContext>(),
            CachePolicyProvider: services.GetService<IKyrolusRepositoryCachePolicyProvider>(),
            PolicyProvider: services.GetService<IKyrolusMartenRepositoryPolicyProvider>(),
            ResiliencePolicy: services.GetService<IKyrolusMartenResiliencePolicy>(),
            Tracing: services.GetService<IKyrolusMartenTracing>());
        var cache = effectiveDeps.CacheProvider;
        var resilience = effectiveDeps.ResiliencePolicy;
        var tracing = effectiveDeps.Tracing;
        var inner = ActivatorUtilities.CreateInstance<KyrolusMartenRepositoryAsync<TSession, TEntity, TKey>>(services, session, effectiveDeps);
        if (!useDecorator) return inner;
        return new KyrolusMartenRepositoryDecorator<TSession, TEntity, TKey>(inner, cache, resilience, tracing);
    }
}
