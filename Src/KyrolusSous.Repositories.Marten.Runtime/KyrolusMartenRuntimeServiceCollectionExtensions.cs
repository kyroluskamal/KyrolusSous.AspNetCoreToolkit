using KyrolusSous.Repositories.Marten.Runtime.EventStore;
using KyrolusSous.Repositories.Marten.Runtime.Projection;
using KyrolusSous.Repositories.Marten.Runtime.Repository;
using KyrolusSous.Repositories.Marten.Runtime.Repository.Decorators;
using KyrolusSous.Repositories.Marten.Runtime.Saga;
using KyrolusSous.Repositories.Marten.Runtime.UnitOfWork;

namespace KyrolusSous.Repositories.Marten.Runtime;

public static class KyrolusMartenRuntimeServiceCollectionExtensions
{
    /// <summary>
    /// Register Marten runtime services (repositories, unit of work, saga, projections, event store).
    /// </summary>
    public static IServiceCollection AddKyrolusMartenRuntime(this IServiceCollection services, Action<KyrolusMartenDaemonOptions>? configureDaemon = null)
    {
        if (configureDaemon != null)
        {
            services.Configure(configureDaemon);
        }

        services.AddScoped(typeof(IKyrolusMartenRepositoryAsync<,,>), typeof(KyrolusMartenRepositoryAsync<,,>));
        services.AddScoped(typeof(IKyrolusMartenSoftDeleteRepositoryAsync<,,>), typeof(KyrolusMartenSoftDeleteRepositoryAsync<,,>));
        services.AddScoped(typeof(IKyrolusMartenUnitOfWork<>), typeof(KyrolusMartenUnitOfWork<>));
        services.AddScoped<IKyrolusMartenEventStore, KyrolusMartenEventStore>();
        services.AddScoped<IKyrolusMartenSagaCoordinator, KyrolusMartenSagaCoordinator>();
        services.AddScoped<IKyrolusMartenProjectionOrchestrator, KyrolusMartenProjectionOrchestrator>();
        return services;
    }

    /// <summary>
    /// Build a decorated repository instance manually (for scenarios where DI can't auto-decorate open generics).
    /// </summary>
    public static IKyrolusMartenRepositoryAsync<TSession, TEntity, TKey> CreateDecoratedRepository<TSession, TEntity, TKey>(
        this IServiceProvider services,
        TSession session,
        KyrolusMartenRepositoryDependencies? deps = null)
        where TSession : IDocumentSession
        where TEntity : class
        where TKey : IEquatable<TKey>
    {
        var cache = services.GetService<IKyrolusMartenCacheProvider>();
        var resilience = services.GetService<IKyrolusMartenResiliencePolicy>();
        var tracing = services.GetService<IKyrolusMartenTracing>();
        var inner = ActivatorUtilities.CreateInstance<KyrolusMartenRepositoryAsync<TSession, TEntity, TKey>>(services, session, deps ?? new KyrolusMartenRepositoryDependencies());
        return new KyrolusMartenRepositoryDecorator<TSession, TEntity, TKey>(inner, cache, resilience, tracing);
    }
}
