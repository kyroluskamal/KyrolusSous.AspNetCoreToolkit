using KyrolusSous.Repositories.Marten.Runtime.EventStore;
using KyrolusSous.Repositories.Marten.Runtime.Projection;
using KyrolusSous.Repositories.Marten.Runtime.Repository;
using KyrolusSous.Repositories.Marten.Runtime.Repository.Decorators;
using KyrolusSous.Repositories.Marten.Runtime.Saga;
using KyrolusSous.Repositories.Marten.Runtime.UnitOfWork;
using KyrolusSous.Repositories.Marten.Abstractions.Authorization;
using KyrolusSous.Repositories.Marten.Abstractions.Cache;
using KyrolusSous.Repositories.Marten.Abstractions.Observer;
using KyrolusSous.Repositories.Marten.Abstractions.Resilience;
using KyrolusSous.Repositories.Marten.Abstractions.SoftDelete;
using KyrolusSous.Repositories.Marten.Abstractions.Tracing;
using KyrolusSous.Repositories.Marten.Abstractions.Validation;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        services.TryAddSingleton(KyrolusMartenNoopObserver.Instance);
        services.TryAddSingleton(KyrolusMartenAllowAllAuthorization.Instance);
        services.TryAddSingleton(KyrolusMartenNoopValidation.Instance);
        services.TryAddSingleton(KyrolusMartenNoSoftDeletePolicy.Instance);
        services.TryAddSingleton(KyrolusMartenNoopCacheProvider.Instance);
        services.TryAddSingleton(KyrolusMartenNoopResiliencePolicy.Instance);
        services.TryAddSingleton(KyrolusMartenNoopTracing.Instance);

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
        var effectiveDeps = deps ?? new KyrolusMartenRepositoryDependencies(
            Observer: services.GetService<IKyrolusMartenObserver>(),
            Authorization: services.GetService<IKyrolusMartenAuthorization>(),
            Validation: services.GetService<IKyrolusMartenValidation>(),
            SoftDeletePolicy: services.GetService<IKyrolusMartenSoftDeletePolicy>(),
            CacheProvider: services.GetService<IKyrolusMartenCacheProvider>(),
            ResiliencePolicy: services.GetService<IKyrolusMartenResiliencePolicy>(),
            Tracing: services.GetService<IKyrolusMartenTracing>());

        var inner = ActivatorUtilities.CreateInstance<KyrolusMartenRepositoryAsync<TSession, TEntity, TKey>>(services, session, effectiveDeps);
        return new KyrolusMartenRepositoryDecorator<TSession, TEntity, TKey>(
            inner,
            effectiveDeps.CacheProvider,
            effectiveDeps.ResiliencePolicy,
            effectiveDeps.Tracing);
    }
}
