using System.Reflection;
using KyrolusSous.CQRS.EF.Behaviors;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.CQRS.EF.Config;

/// <summary>
/// Service collection extensions for registering EF Core CQRS handlers and behaviors.
/// </summary>
public static class MediatorExtensions
{
    /// <summary>
    /// Registers EF Core generic CQRS commands and queries.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsEf(this IServiceCollection services, params Assembly[] assemblies)
    {
        return services;
    }

    /// <summary>
    /// Registers EF Core atomic transaction management pipeline behavior.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsEfTransactions<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusEfTransactionBehavior<,,>).MakeGenericType(typeof(TDbContext))));
        return services;
    }

    /// <summary>
    /// Registers automatic domain events collection and dispatching pipeline behavior for EF Core.
    /// </summary>
    public static IServiceCollection AddKyrolusCqrsEfDomainEvents<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IKyrolusPipelineBehavior<,>), typeof(KyrolusDomainEventsDispatchBehavior<,,>).MakeGenericType(typeof(TDbContext))));
        return services;
    }
}
