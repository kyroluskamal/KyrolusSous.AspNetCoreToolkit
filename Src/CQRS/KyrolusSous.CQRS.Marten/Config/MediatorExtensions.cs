using System.Reflection;
using KyrolusSous.CQRS.Marten.Behaviors;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.CQRS.Marten.Config;

/// <summary>
/// Service collection extensions for registering Marten CQRS handlers and behaviors.
/// </summary>
public static class MediatorExtensions
{
    /// <summary>
    /// Registers Marten generic CQRS commands and queries.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsMarten(this IServiceCollection services, params Assembly[] assemblies)
    {
        return services;
    }

    /// <summary>
    /// Registers Marten atomic session persistence pipeline behavior for commands.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsMartenTransactions(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusMartenTransactionBehavior<,>)));
        return services;
    }

    /// <summary>
    /// Registers Marten domain events collection and dispatching pipeline behavior.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsMartenDomainEvents(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusMartenDomainEventsDispatchBehavior<,>)));
        return services;
    }
}
