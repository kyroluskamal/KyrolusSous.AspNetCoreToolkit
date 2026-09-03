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
        // descriptor via KyrolusMediatorDispatcherRegistration.Install. Resolving it unreplaced
        // means neither was set up, and the message says so instead of letting the container
        // report a missing IKyrolusMediatorDispatcher. A named type rather than a throwing factory
        // lambda, so Install can recognise "nothing configured yet" by its ImplementationType and
        // tell that apart from "the other package already installed a real one".
        services.TryAddSingleton<IKyrolusMediatorDispatcher, KyrolusMediatorDispatcherPlaceholder>();
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

    /// <summary>
    /// Replaces the notification publish strategy with a parallel one capped at
    /// <paramref name="maxDegreeOfParallelism"/> handlers running at once. Use this when a
    /// notification can fan out to enough handlers that unbounded parallelism would exhaust a
    /// connection pool or the thread pool.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDegreeOfParallelism"/> is less than 1.</exception>
    public static IServiceCollection UseKyrolusMediatorBoundedParallelNotifications(this IServiceCollection services, int maxDegreeOfParallelism)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (maxDegreeOfParallelism < 1)
            throw new ArgumentOutOfRangeException(
                nameof(maxDegreeOfParallelism),
                maxDegreeOfParallelism,
                "[KyrolusMediator] At least one handler must be allowed to run at a time.");

        // Not ReplaceNotificationStrategy<T>: that helper builds TStrategy with a parameterless
        // constructor, and this strategy needs the cap threaded through instead.
        services.RemoveAll<IKyrolusNotificationPublishStrategy>();
        services.AddSingleton<IKyrolusNotificationPublishStrategy>(
            _ => new KyrolusBoundedParallelNotificationPublishStrategy(maxDegreeOfParallelism));
        return services;
    }

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

        // A forwarding factory, not a second ServiceDescriptor for KyrolusMediator: two independent
        // registrations of the same concrete type still build two separate instances, one per
        // resolution, which contradicts the documented "resolving either gives the same mediator"
        // on IMediator (see MediatRCompatibility.cs). KyrolusMediator implements both interfaces,
        // so resolving IKyrolusMediator once and casting it is exactly the same object either way.
        services.TryAdd(new ServiceDescriptor(
            typeof(IMediator),
            static sp => (IMediator)sp.GetRequiredService<IKyrolusMediator>(),
            configuration.MediatorLifetime));

        AddBuiltInBehaviors(services);

        switch (configuration.NotificationPublishMode)
        {
            case NotificationPublishMode.Sequential:
                services.UseKyrolusMediatorSequentialNotifications();
                break;
            case NotificationPublishMode.BoundedParallel:
                if (configuration.NotificationPublishMaxDegreeOfParallelism is not { } maxDegreeOfParallelism || maxDegreeOfParallelism < 1)
                    throw new InvalidOperationException(
                        "[KyrolusMediator] NotificationPublishMode.BoundedParallel requires " +
                        $"{nameof(KyrolusMediatorConfiguration.NotificationPublishMaxDegreeOfParallelism)} " +
                        "to be set to a positive number.");
                services.UseKyrolusMediatorBoundedParallelNotifications(maxDegreeOfParallelism);
                break;
        }

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
        // Closed behaviors (AddBehavior<TImplementation>()) are already concrete, closed types - no
        // MakeGenericType involved in registering or resolving them - so these always run.
        //
        // Reads registration.Implementation directly rather than deconstructing into a local -
        // BehaviorRegistration carries the DynamicallyAccessedMembers(PublicConstructors) annotation
        // ServiceDescriptor's constructor needs on the property itself; a deconstructed local would
        // not carry it, and the trimmer would be back to guessing.
        foreach (var registration in configuration.ClosedBehaviors)
            services.Add(new ServiceDescriptor(registration.Service, registration.Implementation, configuration.Lifetime));

        foreach (var registration in configuration.ClosedStreamBehaviors)
            services.Add(new ServiceDescriptor(registration.Service, registration.Implementation, configuration.Lifetime));

        // Open behaviors (AddOpenBehavior(typeof(Foo<,>))) need the container to close them with
        // MakeGenericType the first time one is resolved, which an application published ahead of
        // time cannot do - the exact same reason AddBuiltInBehaviors declines above. Skipped there;
        // KyrolusSous.Mediator.Generator closes the same registrations at compile time instead, for
        // every AddOpenBehavior(typeof(...)) call site it can see (see AppendClosedUserOpenBehaviors),
        // so exactly one of the two sets is ever present, mirroring the built-in four.
        if (RuntimeFeature.IsDynamicCodeSupported)
        {
            foreach (var registration in configuration.OpenBehaviors)
                services.Add(new ServiceDescriptor(registration.Service, registration.Implementation, configuration.Lifetime));

            foreach (var registration in configuration.OpenStreamBehaviors)
                services.Add(new ServiceDescriptor(registration.Service, registration.Implementation, configuration.Lifetime));
        }
    }
}
