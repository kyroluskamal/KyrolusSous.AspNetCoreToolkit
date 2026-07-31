namespace KyrolusSous.Mediator.Runtime.Config;

public static class MediatorExtensions
{
    /// <summary>Handler interfaces where exactly one implementation may claim a given message.</summary>
    private static readonly HashSet<Type> s_singleHandlerInterfaces =
    [
        typeof(IKyrolusRequestHandler<,>),
        typeof(IKyrolusRequestHandler<>),
        typeof(IKyrolusQueryHandler<,>),
        typeof(IKyrolusCommandHandler<,>),
        typeof(IKyrolusCommandHandler<>),
        typeof(IKyrolusStreamRequestHandler<,>)
    ];

    /// <summary>Interfaces where any number of implementations may be registered together.</summary>
    private static readonly HashSet<Type> s_multiHandlerInterfaces =
    [
        typeof(INotificationHandler<>),
        typeof(IKyrolusPipelineBehavior<,>),
        typeof(IKyrolusStreamPipelineBehavior<,>),
        typeof(IKyrolusRequestPreProcessor<>),
        typeof(IKyrolusRequestPostProcessor<,>),
        typeof(IKyrolusRequestExceptionAction<,>),
        typeof(IKyrolusRequestExceptionHandler<,,>)
    ];

    public static void AddKyrolusMediatorSender(this IServiceCollection services)
    {
        services.TryAddScoped<IKyrolusMediatorSender, KyrolusMediatorSender>();
        services.TryAddSingleton<IMediatorDispatcher, KyrolusReflectionDispatcher>();
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

    private static IServiceCollection ReplaceNotificationStrategy<TStrategy>(this IServiceCollection services)
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

        services.AddKyrolusMediatorSender();
        services.AddKyrolusMediatorPublisher();

        services.TryAdd(new ServiceDescriptor(typeof(IKyrolusMediator), typeof(KyrolusMediator), configuration.MediatorLifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IMediator), typeof(KyrolusMediator), configuration.MediatorLifetime));

        AddBuiltInBehaviors(services);

        if (configuration.NotificationPublishMode == NotificationPublishMode.Sequential)
            services.UseKyrolusMediatorSequentialNotifications();

        RegisterConfiguredBehaviors(services, configuration);

        if (configuration.AssembliesToScan.Count > 0)
            RegisterKyrolusMediatorHandlers(services, [.. configuration.AssembliesToScan], configuration);

        return services;
    }

    private static void AddBuiltInBehaviors(IServiceCollection services)
    {
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

    /// <summary>Registers the mediator and scans the given assemblies for handlers.</summary>
    public static IServiceCollection AddKyrolusMediatorFromAssemblies(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies is null || assemblies.Length == 0)
            throw new ArgumentException("At least one assembly is required.", nameof(assemblies));
        return services.AddKyrolusMediator(configuration => configuration.RegisterServicesFromAssemblies(assemblies));
    }

    private static void RegisterKyrolusMediatorHandlers(
        IServiceCollection services,
        Assembly[] assemblies,
        KyrolusMediatorConfiguration configuration)
    {
        // Tracks which implementation already claimed a single-handler service type, so a second
        // one can be reported with both names instead of silently losing.
        var claimed = new Dictionary<Type, Type>();

        foreach (var assembly in assemblies)
        {
            foreach (var typeInfo in GetLoadableTypes(assembly))
            {
                if (!typeInfo.IsClass || typeInfo.IsAbstract) continue;

                var implType = typeInfo.AsType();
                foreach (var iface in typeInfo.ImplementedInterfaces)
                {
                    if (!iface.IsGenericType) continue;

                    var ifaceDef = iface.GetGenericTypeDefinition();
                    if (s_singleHandlerInterfaces.Contains(ifaceDef))
                        RegisterHandler(services, iface, ifaceDef, implType, configuration, claimed);
                    else if (s_multiHandlerInterfaces.Contains(ifaceDef))
                        RegisterMultiHandler(services, iface, ifaceDef, implType, configuration);
                }
            }
        }
    }

    /// <summary>
    /// Enumerates the types an assembly can actually load. One unresolvable dependency would
    /// otherwise make the entire scan throw.
    /// </summary>
    private static IEnumerable<TypeInfo> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.DefinedTypes;
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types
                .Where(type => type is not null)
                .Select(type => type!.GetTypeInfo());
        }
    }

    private static void RegisterHandler(
        IServiceCollection services,
        Type iface,
        Type ifaceDef,
        Type implType,
        KyrolusMediatorConfiguration configuration,
        Dictionary<Type, Type> claimed)
    {
        if (implType.ContainsGenericParameters)
        {
            services.TryAdd(new ServiceDescriptor(ifaceDef, implType, configuration.Lifetime));
            return;
        }

        if (claimed.TryGetValue(iface, out var existing))
        {
            if (existing == implType)        return;

            if (configuration.ThrowOnDuplicateRequestHandlers)
                throw new InvalidOperationException(
                    $"[KyrolusMediator] Two handlers are registered for {iface}: " +
                    $"{existing.FullName} and {implType.FullName}. A request must have exactly one handler. " +
                    $"Remove one, or set {nameof(KyrolusMediatorConfiguration.ThrowOnDuplicateRequestHandlers)} to false to keep the first.");
            return;
        }
        claimed[iface] = implType;
        services.TryAdd(new ServiceDescriptor(iface, implType, configuration.Lifetime));
    }

    private static void RegisterMultiHandler(
        IServiceCollection services,
        Type iface,
        Type ifaceDef,
        Type implType,
        KyrolusMediatorConfiguration configuration)
    {
        if (implType.ContainsGenericParameters)
        {
            services.TryAddEnumerable(new ServiceDescriptor(ifaceDef, implType, configuration.Lifetime));
            return;
        }

        services.TryAddEnumerable(new ServiceDescriptor(iface, implType, configuration.Lifetime));
    }
}
