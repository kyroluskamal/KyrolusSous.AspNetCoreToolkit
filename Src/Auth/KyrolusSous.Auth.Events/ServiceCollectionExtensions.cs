using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Auth.Events;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the central Kyrolus auth event dispatcher sink.
    /// </summary>
    public static IServiceCollection AddKyrolusAuthEvents(this IServiceCollection services)
    {
        services.TryAddScoped<IKyrolusAuthEventSink, KyrolusAuthEventDispatcher>();
        return services;
    }

    /// <summary>
    /// Registers an event handler for a specific authentication or security event.
    /// </summary>
    public static IServiceCollection AddKyrolusAuthEventHandler<TEvent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services)
        where TEvent : IKyrolusAuthEvent
        where THandler : class, IKyrolusAuthEventHandler<TEvent>
    {
        services.AddScoped<IKyrolusAuthEventHandler<TEvent>, THandler>();
        return services;
    }
}
