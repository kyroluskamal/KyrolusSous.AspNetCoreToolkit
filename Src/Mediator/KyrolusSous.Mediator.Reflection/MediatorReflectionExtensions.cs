using System.Diagnostics.CodeAnalysis;
using KyrolusSous.Mediator.Runtime.Config;

namespace KyrolusSous.Mediator.Reflection;

/// <summary>
/// Turns on the reflection half of the mediator: handlers found by scanning assemblies, and
/// dispatch that closes generic types at runtime.
/// </summary>
/// <remarks>
/// This is the alternative to <c>KyrolusSous.Mediator.Generator</c>, not a companion to it. Pick
/// one: the generator resolves everything at compile time and can be published with NativeAOT;
/// this package resolves it at runtime and cannot.
/// </remarks>
public static class MediatorReflectionExtensions
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

    /// <summary>
    /// Registers reflection-based dispatch and scans whatever assemblies the mediator
    /// configuration was given.
    /// </summary>
    /// <remarks>Call after <c>AddKyrolusMediator</c>: it reads the configuration that produced.</remarks>
    [RequiresDynamicCode("Reflection-based dispatch closes generic types at runtime. Use KyrolusSous.Mediator.Generator for an application published ahead of time.")]
    [RequiresUnreferencedCode("Scanning discovers handlers by name and interface, which trimming may remove.")]
    public static IServiceCollection AddKyrolusMediatorReflection(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Replace, not TryAdd: the runtime registers a placeholder that throws, precisely so that
        // reaching it means neither half was set up.
        services.Replace(ServiceDescriptor.Singleton<IMediatorDispatcher, KyrolusReflectionDispatcher>());

        services.TryAddSingleton<IKyrolusPipelineWrapperSource, ReflectionPipelineWrapperSource>();
        services.TryAddSingleton<IKyrolusNotificationDispatchSource, ReflectionNotificationDispatchSource>();
        services.TryAddSingleton<IKyrolusRequestExceptionDispatchSource, ReflectionRequestExceptionDispatchSource>();

        var configuration = FindConfiguration(services)
            ?? throw new InvalidOperationException(
                "[KyrolusMediator] AddKyrolusMediatorReflection() must be called after AddKyrolusMediator(), " +
                "which is what records the assemblies to scan and the lifetimes to use.");

        if (configuration.AssembliesToScan.Count > 0)
            RegisterKyrolusMediatorHandlers(services, [.. configuration.AssembliesToScan], configuration);

        return services;
    }

    /// <summary>Registers the mediator with reflection enabled, scanning the given assemblies.</summary>
    [RequiresDynamicCode("Reflection-based dispatch closes generic types at runtime.")]
    [RequiresUnreferencedCode("Scanning discovers handlers by name and interface, which trimming may remove.")]
    public static IServiceCollection AddKyrolusMediatorFromAssemblies(
        this IServiceCollection services,
        params Assembly[] assemblies)
        => services.AddKyrolusMediatorFromAssemblies(_ => { }, assemblies);

    /// <summary>Registers the mediator with reflection enabled, scanning the given assemblies with custom configuration.</summary>
    [RequiresDynamicCode("Reflection-based dispatch closes generic types at runtime.")]
    [RequiresUnreferencedCode("Scanning discovers handlers by name and interface, which trimming may remove.")]
    public static IServiceCollection AddKyrolusMediatorFromAssemblies(
        this IServiceCollection services,
        Action<KyrolusMediatorConfiguration> configure,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (assemblies is null || assemblies.Length == 0)
            throw new ArgumentException("At least one assembly is required.", nameof(assemblies));

        services.AddKyrolusMediator(configuration =>
        {
            configure(configuration);
            configuration.RegisterServicesFromAssemblies(assemblies);
        });
        return services.AddKyrolusMediatorReflection();
    }

    private static KyrolusMediatorConfiguration? FindConfiguration(IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
            if (services[i].ServiceType == typeof(KyrolusMediatorConfiguration)
                && services[i].ImplementationInstance is KyrolusMediatorConfiguration configuration)
                return configuration;
        return null;
    }

    [RequiresUnreferencedCode("Enumerates every type in the assembly, which trimming may remove.")]
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
    [RequiresUnreferencedCode("Enumerates every type in the assembly, which trimming may remove.")]
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
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implType,
        KyrolusMediatorConfiguration configuration,
        Dictionary<Type, Type> claimed)
    {
        if (implType.ContainsGenericParameters)
        {
            if (claimed.TryGetValue(ifaceDef, out var existingGeneric))
            {
                if (existingGeneric == implType) return;

                if (configuration.ThrowOnDuplicateRequestHandlers)
                    throw new InvalidOperationException(
                        $"[KyrolusMediator] Two generic handlers are registered for {ifaceDef}: " +
                        $"{existingGeneric.FullName} and {implType.FullName}. A request must have exactly one handler.");
                return;
            }

            claimed[ifaceDef] = implType;
            services.TryAdd(new ServiceDescriptor(ifaceDef, implType, configuration.Lifetime));
            return;
        }

        if (claimed.TryGetValue(iface, out var existing))
        {
            if (existing == implType) return;

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
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implType,
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
