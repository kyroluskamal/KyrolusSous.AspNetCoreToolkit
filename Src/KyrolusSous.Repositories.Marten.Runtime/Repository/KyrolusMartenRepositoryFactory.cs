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
        var cache = services.GetService<IKyrolusMartenCacheProvider>();
        var resilience = services.GetService<IKyrolusMartenResiliencePolicy>();
        var tracing = services.GetService<IKyrolusMartenTracing>();
        var inner = ActivatorUtilities.CreateInstance<KyrolusMartenRepositoryAsync<TSession, TEntity, TKey>>(services, session, deps ?? new KyrolusMartenRepositoryDependencies());
        if (!useDecorator) return inner;
        return new KyrolusMartenRepositoryDecorator<TSession, TEntity, TKey>(inner, cache, resilience, tracing);
    }
}
