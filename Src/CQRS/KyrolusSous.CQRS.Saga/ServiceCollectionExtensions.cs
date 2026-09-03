using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.CQRS.Saga;

/// <summary>
/// Registration entry points for the Saga / Process Manager package.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the saga coordinator and its registry, with the in-memory store as the default.
    /// Call once; call <see cref="AddKyrolusSaga{TSagaDefinition}"/> for each saga definition the
    /// application defines.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsSaga(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IKyrolusSagaStore, InMemorySagaStore>();
        services.TryAddSingleton<IKyrolusSagaDefinitionRegistry, KyrolusSagaDefinitionRegistry>();
        services.TryAddSingleton<IKyrolusSagaCoordinator, KyrolusSagaCoordinator>();
        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TSagaDefinition"/> so the coordinator can start it and, on
    /// resume, find it again by its <see cref="IKyrolusSagaDefinition.SagaName"/>.
    /// </summary>
    public static IServiceCollection AddKyrolusSaga<TSagaDefinition>(this IServiceCollection services)
        where TSagaDefinition : class, IKyrolusSagaDefinition
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TSagaDefinition>();
        services.AddSingleton<IKyrolusSagaDefinition>(sp => sp.GetRequiredService<TSagaDefinition>());
        return services;
    }

    /// <summary>
    /// Replaces the default in-memory saga store with <typeparamref name="TStore"/> - use this for
    /// any deployment where a saga must survive a process restart.
    /// </summary>
    public static IServiceCollection AddKyrolusSagaStore<TStore>(this IServiceCollection services)
        where TStore : class, IKyrolusSagaStore
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IKyrolusSagaStore>();
        services.AddSingleton<IKyrolusSagaStore, TStore>();
        return services;
    }
}
