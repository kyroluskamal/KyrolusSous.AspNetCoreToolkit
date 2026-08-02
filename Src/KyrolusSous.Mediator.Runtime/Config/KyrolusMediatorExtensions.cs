using System.Diagnostics.CodeAnalysis;

namespace KyrolusSous.Mediator.Runtime.Config;

/// <summary>
/// Registration entry points for the mediator.
/// </summary>
/// <remarks>
/// Nothing here uses reflection, which is what lets this assembly declare
/// <c>IsAotCompatible</c> without a single suppression. Discovering handlers by scanning
/// assemblies, and dispatching to them by closing interfaces at runtime, both live in
/// <c>KyrolusSous.Mediator.Reflection</c> - a separate package precisely so an application that
/// never wants them never has the code in its graph.
/// </remarks>
public static class MediatorExtensions
{
    public static void AddKyrolusMediatorSender(this IServiceCollection services)
    {
        services.TryAddScoped<IKyrolusMediatorSender, KyrolusMediatorSender>();

        // A placeholder rather than a default implementation. Something has to supply the dispatch,
        // and both things that can - the generator and the reflection package - replace this
        // descriptor. Resolving it means neither was set up, and the message says so instead of
        // letting the container report a missing IMediatorDispatcher.
        services.TryAddSingleton<IMediatorDispatcher>(static _ => throw new InvalidOperationException(
            "[KyrolusMediator] No dispatcher is registered. Reference KyrolusSous.Mediator.Generator " +
            "and call AddKyrolusMediatorGeneratedDispatcher(), or reference " +
            "KyrolusSous.Mediator.Reflection and call AddKyrolusMediatorReflection()."));
    }

    public static void AddKyrolusMediatorPublisher(this IServiceCollection services)
    {
        services.TryAddScoped<IKyrolusMediatorPublisher, KyrolusMediatorPublisher>();
        services.TryAddSingleton<IKyrolusNotificationPublishStrategy, KyrolusParallelNotificationPublishStrategy>();
    }

    /// <summary>Replaces the notification publish strategy with the parallel one.</summary>
    public static IServiceCollection UseKyrolusMediatorParallelNotifications(this IServiceCollection services)
        => services.ReplaceNotificationStrategy<KyrolusParallelNotificationPublishStrategy>();

    /// <summary>
    /// Replaces the notification publish strategy with the sequential one. Use this when
    /// notification handlers share a scoped resource such as a <c>DbContext</c>.
    /// </summary>
    public static IServiceCollection UseKyrolusMediatorSequentialNotifications(this IServiceCollection services)
        => services.ReplaceNotificationStrategy<KyrolusSequentialNotificationPublishStrategy>();

    private static IServiceCollection ReplaceNotificationStrategy<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStrategy>(
        this IServiceCollection services)
        where TStrategy : class, IKyrolusNotificationPublishStrategy
    {
        ArgumentNullException.ThrowIfNull(services);

        // Replace rather than Add: stacking registrations would leave GetServices returning both
        // strategies, and which one wins would silently depend on registration order.
        services.RemoveAll<IKyrolusNotificationPublishStrategy>();
        services.AddSingleton<IKyrolusNotificationPublishStrategy, TStrategy>();
        return services;
    }

    /// <summary>Registers the mediator, its dispatcher and the built-in pipeline behaviors.</summary>
    public static void AddKyrolusMediator(this IServiceCollection services)
        => services.AddKyrolusMediator(static _ => { });

    /// <summary>
    /// Registers the mediator with explicit configuration - assemblies to scan, behaviors to run,
    /// lifetimes, and how notifications are published.
    /// </summary>
    public static IServiceCollection AddKyrolusMediator(
        this IServiceCollection services,
        Action<KyrolusMediatorConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var configuration = new KyrolusMediatorConfiguration();
        configure(configuration);

        // Kept in the collection so AddKyrolusMediatorReflection can read the assemblies and
        // lifetimes it was given. Scanning cannot happen here: it needs reflection, which this
        // assembly deliberately does not contain.
        services.TryAddSingleton(configuration);

        services.AddKyrolusMediatorSender();
        services.AddKyrolusMediatorPublisher();

        services.TryAdd(new ServiceDescriptor(typeof(IKyrolusMediator), typeof(KyrolusMediator), configuration.MediatorLifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IMediator), typeof(KyrolusMediator), configuration.MediatorLifetime));

        AddBuiltInBehaviors(services);

        if (configuration.NotificationPublishMode == NotificationPublishMode.Sequential)
            services.UseKyrolusMediatorSequentialNotifications();

        RegisterConfiguredBehaviors(services, configuration);

        return services;
    }

    /// <summary>
    /// Registers the behaviors every pipeline gets, as open generics.
    /// </summary>
    /// <remarks>
    /// An open generic registration leaves the container to close the behavior over the request and
    /// response types the first time one is resolved, and it does that with <c>MakeGenericType</c>.
    /// An application published ahead of time refuses outright as soon as a type argument is a value
    /// type - a query returning <c>int</c> is enough - so these are registered only where the
    /// runtime can still produce code on demand.
    /// <para>
    /// The generator emits the same four behaviors closed over every pair it found, and registers
    /// those instead when this method declines. The two are mutually exclusive on the same
    /// condition, so exactly one set is ever present and no behavior runs twice.
    /// </para>
    /// </remarks>
    private static void AddBuiltInBehaviors(IServiceCollection services)
    {
        if (!RuntimeFeature.IsDynamicCodeSupported) return;

        services.TryAddEnumerable(ServiceDescriptor.Transient(
            typeof(IKyrolusPipelineBehavior<,>),
            typeof(KyrolusRequestExceptionProcessorBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Transient(
            typeof(IKyrolusPipelineBehavior<,>),
            typeof(KyrolusRequestPreProcessorBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Transient(
            typeof(IKyrolusPipelineBehavior<,>),
            typeof(KyrolusRequestPostProcessorBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Transient(
            typeof(IKyrolusStreamPipelineBehavior<,>),
            typeof(KyrolusStreamPassThroughBehavior<,>)));
    }

    private static void RegisterConfiguredBehaviors(IServiceCollection services, KyrolusMediatorConfiguration configuration)
    {
        // Add, not TryAddEnumerable: the caller listed these explicitly, so registration order is
        // the intended execution order and a deliberate duplicate is legal.
        foreach (var (service, implementation) in configuration.ClosedBehaviors)
            services.Add(new ServiceDescriptor(service, implementation, configuration.Lifetime));

        foreach (var (service, implementation) in configuration.OpenBehaviors)
            services.Add(new ServiceDescriptor(service, implementation, configuration.Lifetime));

        foreach (var (service, implementation) in configuration.ClosedStreamBehaviors)
            services.Add(new ServiceDescriptor(service, implementation, configuration.Lifetime));

        foreach (var (service, implementation) in configuration.OpenStreamBehaviors)
            services.Add(new ServiceDescriptor(service, implementation, configuration.Lifetime));
    }
}
