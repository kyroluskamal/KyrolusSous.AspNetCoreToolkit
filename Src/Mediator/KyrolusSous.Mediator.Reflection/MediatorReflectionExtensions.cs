using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using KyrolusSous.Mediator.Abstractions.Compatibility;
using KyrolusSous.Mediator.Abstractions.Interfaces;
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
    /// <remarks>
    /// The MediatR compatibility <c>INotificationHandler&lt;&gt;</c> is deliberately absent: it
    /// inherits <see cref="IKyrolusNotificationHandler{TNotification}"/>, so a class implementing it
    /// already shows up here under the native interface via <c>ImplementedInterfaces</c> - listing
    /// both meant a compat-ported handler got registered twice (once per interface) and ran twice
    /// per notification. Request/command/query handlers already followed this rule; this list did
    /// not, which is what let the duplicate slip in.
    /// </remarks>
    private static readonly HashSet<Type> s_multiHandlerInterfaces =
    [
        typeof(IKyrolusNotificationHandler<>),
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

        // Goes through the shared guard rather than a bare Replace: the runtime registers a
        // placeholder that throws, precisely so that reaching it means neither half was set up, but
        // a bare Replace could not tell that apart from "AddKyrolusMediatorGeneratedDispatcher()
        // already installed the generated one" - it would silently discard whichever ran first.
        // See the remarks on KyrolusMediatorDispatcherRegistration for why that matters.
        KyrolusMediatorDispatcherRegistration.Install<KyrolusReflectionDispatcher>(services, nameof(AddKyrolusMediatorReflection));
        services.Replace(ServiceDescriptor.Singleton<IMediatorDispatcher>(static sp => (IMediatorDispatcher)sp.GetRequiredService<IKyrolusMediatorDispatcher>()));

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
        // Tracks which implementation already claimed a single-handler shape, so a second one can
        // be reported with both names instead of silently losing. Keyed by a canonical shape string
        // rather than by Type - see BuildGenericShapeKey for why a Type key is not enough.
        var claimed = new Dictionary<string, Type>(StringComparer.Ordinal);

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
        Dictionary<string, Type> claimed)
    {
        if (implType.ContainsGenericParameters)
        {
            RequireMatchingArity(ifaceDef, implType);

            // Keying on ifaceDef alone (e.g. IKyrolusCommandHandler<,>) would treat every open-generic
            // handler for that interface as "the same slot", even though CreateHandler<T> : IKyrolusCommandHandler<CreateCommand<T>, T>
            // and UpdateHandler<T> : IKyrolusCommandHandler<UpdateCommand<T>, T> target completely
            // different request shapes and must both be allowed to exist. The key has to describe the
            // shape the handler actually closes over, not just which handler interface it implements.
            var shapeKey = BuildGenericShapeKey(ifaceDef, iface, implType.GetGenericArguments());

            if (claimed.TryGetValue(shapeKey, out var existingGeneric))
            {
                if (existingGeneric == implType) return;

                if (configuration.ThrowOnDuplicateRequestHandlers)
                    throw new InvalidOperationException(
                        $"[KyrolusMediator] Two generic handlers are registered for the same request shape " +
                        $"under {ifaceDef}: {existingGeneric.FullName} and {implType.FullName}. A request must have exactly one handler.");
                return;
            }

            claimed[shapeKey] = implType;
            // Not TryAdd: TryAdd(single) no-ops as soon as ANY descriptor exists for ifaceDef, which
            // would silently drop every open-generic handler after the first one registered for this
            // interface - even though each targets a distinct request shape (already verified distinct
            // above) and DI resolves a closed request type by matching whichever open registration's
            // generic shape actually unifies with it. AddOpenGenericHandlerIfMissing still keeps this
            // idempotent if the caller runs registration twice.
            AddOpenGenericHandlerIfMissing(services, ifaceDef, implType, configuration.Lifetime);
            return;
        }

        if (claimed.TryGetValue(iface.ToString(), out var existing))
        {
            if (existing == implType) return;

            if (configuration.ThrowOnDuplicateRequestHandlers)
                throw new InvalidOperationException(
                    $"[KyrolusMediator] Two handlers are registered for {iface}: " +
                    $"{existing.FullName} and {implType.FullName}. A request must have exactly one handler. " +
                    $"Remove one, or set {nameof(KyrolusMediatorConfiguration.ThrowOnDuplicateRequestHandlers)} to false to keep the first.");
            return;
        }

        claimed[iface.ToString()] = implType;
        services.TryAdd(new ServiceDescriptor(iface, implType, configuration.Lifetime));
    }

    /// <summary>
    /// Builds a canonical string describing the request/response shape an open-generic handler
    /// closes over, so two handlers can be compared for "same shape" without depending on Type
    /// equality between generic-parameter placeholders that belong to different classes (which are
    /// never equal even when the shapes they stand in for are identical).
    /// </summary>
    private static string BuildGenericShapeKey(Type ifaceDef, Type iface, Type[] implOwnParameters)
    {
        var args = iface.GetGenericArguments();
        var parts = new string[args.Length];
        for (var i = 0; i < args.Length; i++)
            parts[i] = CanonicalizeShape(args[i], implOwnParameters);
        return ifaceDef.FullName + "<" + string.Join(",", parts) + ">";
    }

    /// <summary>
    /// Renders one generic argument of a handler's interface as a shape fragment: the handler's own
    /// type parameter (wherever it appears, however nested) becomes a position-erased placeholder, so
    /// "CreateCommand&lt;T&gt;" from one class and "CreateCommand&lt;T&gt;" from another both render
    /// identically, while "CreateCommand&lt;T&gt;" and "UpdateCommand&lt;T&gt;" do not.
    /// </summary>
    private static string CanonicalizeShape(Type type, Type[] implOwnParameters)
    {
        if (Array.IndexOf(implOwnParameters, type) >= 0)
            return "#";

        if (type.IsGenericParameter)
            return "#?" + type.Name;

        if (!type.IsGenericType)
            return type.FullName ?? type.Name;

        var definition = type.GetGenericTypeDefinition();
        var arguments = type.GetGenericArguments();
        var parts = new string[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
            parts[i] = CanonicalizeShape(arguments[i], implOwnParameters);
        return definition.FullName + "<" + string.Join(",", parts) + ">";
    }

    /// <summary>
    /// Registers an open-generic handler unless a descriptor for the exact same
    /// (service type, implementation type) pair is already present. Deliberately not
    /// <c>TryAdd</c>: that overload skips registration as soon as any descriptor exists for
    /// <paramref name="serviceType"/> at all, which would silently drop every open-generic handler
    /// after the first one seen for a given handler interface - even though several distinct
    /// implementation types legitimately share the same open service type, one per request shape.
    /// Checking the pair instead keeps repeated registration calls idempotent without that side effect.
    /// </summary>
    private static void AddOpenGenericHandlerIfMissing(
        IServiceCollection services,
        Type serviceType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType,
        ServiceLifetime lifetime)
    {
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == serviceType && descriptor.ImplementationType == implementationType)
                return;
        }

        services.Add(new ServiceDescriptor(serviceType, implementationType, lifetime));
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
            RequireMatchingArity(ifaceDef, implType);
            services.TryAddEnumerable(new ServiceDescriptor(ifaceDef, implType, configuration.Lifetime));
            return;
        }

        services.TryAddEnumerable(new ServiceDescriptor(iface, implType, configuration.Lifetime));
    }

    /// <summary>
    /// Rejects an open-generic handler whose own type-parameter count does not match the handler
    /// interface's, with a message that explains why - rather than letting registration succeed and
    /// leaving the confusing ".NET's built-in DI container can only close an open generic service by
    /// applying the requested arguments straight onto the implementation's own type parameters, in
    /// order" failure to surface later, at <c>BuildServiceProvider()</c> or first resolution, with no
    /// hint that a Kyrolus handler registration is the actual cause.
    /// </summary>
    /// <remarks>
    /// This is a real constraint of the framework's container, not a Kyrolus limitation: an open
    /// generic implementation such as <c>CreateHandler&lt;T&gt; : IKyrolusCommandHandler&lt;CreateCommand&lt;T&gt;, T&gt;</c>
    /// (one type parameter, wrapped into the interface's two arguments) cannot be closed by
    /// <c>Microsoft.Extensions.DependencyInjection</c>'s default container at all - only a direct,
    /// arity-matching passthrough like <c>Handler&lt;TRequest, TResponse&gt; : IKyrolusRequestHandler&lt;TRequest, TResponse&gt;</c>
    /// can. A handler that needs the wrapped shape has to be written as a closed type per concrete
    /// request instead, or the application should reference <c>KyrolusSous.Mediator.Generator</c>,
    /// which closes such handlers at compile time rather than asking the container to do it at runtime.
    /// </remarks>
    /// <summary>Internal rather than private solely so the unit test suite can exercise it directly without polluting a shared, whole-assembly-scanned test fixture.</summary>
    internal static void RequireMatchingArity(Type ifaceDef, Type implType)
    {
        var implArity = implType.GetGenericArguments().Length;
        var ifaceArity = ifaceDef.GetGenericArguments().Length;
        if (implArity == ifaceArity) return;

        throw new InvalidOperationException(
            $"[KyrolusMediator] {implType.FullName} declares {implArity} type parameter(s) but implements " +
            $"{ifaceDef} whose open form needs {ifaceArity}. The built-in DI container can only close an " +
            "open-generic handler by applying the request's type arguments straight onto the handler's own " +
            "type parameters, in order - it cannot resolve a handler that wraps, fixes, or reorders them. " +
            "Give the handler exactly one type parameter per interface argument, applied directly (for " +
            "example `Handler<TRequest, TResponse> : IKyrolusRequestHandler<TRequest, TResponse>`), or write " +
            "a closed handler per concrete request instead.");
    }
}
