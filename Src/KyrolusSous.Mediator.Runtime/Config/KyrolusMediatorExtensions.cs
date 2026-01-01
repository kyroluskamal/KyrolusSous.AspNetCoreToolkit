
namespace KyrolusSous.Mediator.Runtime.Config;

public static class MediatorExtensions
{
    public static void AddKyrolusMediatorSender(this IServiceCollection services)
    {
        services.TryAddScoped<IKyrolusMediatorSender, KyrolusMediatorSender>();
        services.TryAddSingleton<IGeneratedDispatcher, KyrolusReflectionDispatcher>();
    }

    public static void AddKyrolusMediatorPublisher(this IServiceCollection services)
    {
        services.TryAddScoped<IKyrolusMediatorPublisher, KyrolusMediatorPublisher>();
        services.TryAddSingleton<IKyrolusNotificationPublishStrategy, KyrolusParallelNotificationPublishStrategy>();
    }

    public static IServiceCollection UseKyrolusMediatorParallelNotifications(this IServiceCollection services)
    {
        services.AddSingleton<IKyrolusNotificationPublishStrategy, KyrolusParallelNotificationPublishStrategy>();
        return services;
    }

    public static IServiceCollection UseKyrolusMediatorSequentialNotifications(this IServiceCollection services)
    {
        services.AddSingleton<IKyrolusNotificationPublishStrategy, KyrolusSequentialNotificationPublishStrategy>();
        return services;
    }

    public static void AddKyrolusMediator(this IServiceCollection services)
    {
        services.AddKyrolusMediatorSender();
        services.AddKyrolusMediatorPublisher();
        services.TryAddScoped<IKyrolusMediator, KyrolusMediator>();
        services.TryAddScoped<KyrolusSous.Mediator.Abstractions.Compatibility.IMediator, KyrolusMediator>();
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

    public static IServiceCollection AddKyrolusMediatorFromAssemblies(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies is null || assemblies.Length == 0)
        {
            throw new ArgumentException("At least one assembly is required.", nameof(assemblies));
        }

        services.AddKyrolusMediator();
        RegisterKyrolusMediatorHandlers(services, assemblies);
        return services;
    }

    private static void RegisterKyrolusMediatorHandlers(IServiceCollection services, Assembly[] assemblies)
    {
        var singleHandlerInterfaces = new HashSet<Type>
        {
            typeof(IKyrolusRequestHandler<,>),
            typeof(IKyrolusRequestHandler<>),
            typeof(IKyrolusQueryHandler<,>),
            typeof(IKyrolusCommandHandler<,>),
            typeof(IKyrolusCommandHandler<>),
            typeof(IKyrolusStreamRequestHandler<,>)
        };

        var multiHandlerInterfaces = new HashSet<Type>
        {
            typeof(INotificationHandler<>),
            typeof(IKyrolusPipelineBehavior<,>),
            typeof(IKyrolusStreamPipelineBehavior<,>),
            typeof(IKyrolusRequestPreProcessor<>),
            typeof(IKyrolusRequestPostProcessor<,>),
            typeof(IKyrolusRequestExceptionAction<,>),
            typeof(IKyrolusRequestExceptionHandler<,,>)
        };

        foreach (var assembly in assemblies)
        {
            foreach (var typeInfo in assembly.DefinedTypes)
            {
                if (!typeInfo.IsClass || typeInfo.IsAbstract) continue;

                var implType = typeInfo.AsType();
                foreach (var iface in typeInfo.ImplementedInterfaces)
                {
                    if (!iface.IsGenericType) continue;

                    var ifaceDef = iface.GetGenericTypeDefinition();
                    if (singleHandlerInterfaces.Contains(ifaceDef))
                    {
                        RegisterHandler(services, iface, ifaceDef, implType);
                    }
                    else if (multiHandlerInterfaces.Contains(ifaceDef))
                    {
                        RegisterMultiHandler(services, iface, ifaceDef, implType);
                    }
                }
            }
        }
    }

    private static void RegisterHandler(IServiceCollection services, Type iface, Type ifaceDef, Type implType)
    {
        if (implType.ContainsGenericParameters)
        {
            services.TryAddTransient(ifaceDef, implType);
            return;
        }

        services.TryAddTransient(iface, implType);
    }

    private static void RegisterMultiHandler(IServiceCollection services, Type iface, Type ifaceDef, Type implType)
    {
        if (implType.ContainsGenericParameters)
        {
            services.TryAddEnumerable(ServiceDescriptor.Transient(ifaceDef, implType));
            return;
        }

        services.TryAddEnumerable(ServiceDescriptor.Transient(iface, implType));
    }
}
