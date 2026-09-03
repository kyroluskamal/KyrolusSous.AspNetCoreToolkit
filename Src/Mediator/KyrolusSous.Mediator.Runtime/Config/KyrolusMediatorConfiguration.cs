namespace KyrolusSous.Mediator.Runtime.Config;

/// <summary>
/// How notification handlers are executed when no per-call strategy is supplied.
/// </summary>
public enum NotificationPublishMode
{
    /// <summary>All handlers start together and are awaited with <c>Task.WhenAll</c>. Fastest, but
    /// handlers must not share non-thread-safe state such as a single <c>DbContext</c>.</summary>
    Parallel = 0,

    /// <summary>Handlers run one after another. Slower, but safe to share a scoped resource.</summary>
    Sequential = 1,

    /// <summary>
    /// Handlers run in parallel, capped at <see cref="KyrolusMediatorConfiguration.NotificationPublishMaxDegreeOfParallelism"/>
    /// running at once. Use this when a notification can fan out to enough handlers that
    /// unbounded parallelism would exhaust a connection pool or the thread pool.
    /// </summary>
    BoundedParallel = 2
}

/// <summary>
/// One user-supplied pipeline behavior registration: which service interface it answers to, and
/// which concrete type implements it.
/// </summary>
/// <remarks>
/// A dedicated type rather than a plain <c>(Type Service, Type Implementation)</c> tuple. A tuple
/// field cannot carry <see cref="DynamicallyAccessedMembersAttribute"/>, and
/// <see cref="KyrolusMediatorConfiguration.AddBehavior(Type)"/> and
/// <see cref="KyrolusMediatorConfiguration.AddOpenBehavior"/> both receive that annotation on their
/// own <c>Type</c> parameter - it tells the trimmer the type's public constructors must survive,
/// because <see cref="ServiceDescriptor"/> needs them to build the behavior later. Storing the
/// value in a tuple and reading it back out - which is exactly what
/// <c>MediatorExtensions.RegisterConfiguredBehaviors</c> does - silently drops that guarantee, and
/// a NativeAOT publish (not the per-project build-time analyzer, which cannot see that far) reports
/// it as IL2077. <see cref="Implementation"/> carries the same annotation directly, so the
/// guarantee survives the round trip.
/// </remarks>
internal readonly struct BehaviorRegistration
{
    public BehaviorRegistration(
        Type service,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementation)
    {
        Service = service;
        Implementation = implementation;
    }

    public Type Service { get; }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type Implementation { get; }
}

/// <summary>
/// Options for <c>AddKyrolusMediator</c>. Collects the assemblies to scan, the behaviors to run,
/// and the service lifetimes to register everything under.
/// </summary>
public sealed class KyrolusMediatorConfiguration
{
    /// <summary>
    /// What a behavior type must keep for these methods to work after trimming: its interfaces, to
    /// recognise which behavior it is, and its constructors, so the container can build it.
    /// </summary>
    private const DynamicallyAccessedMemberTypes BehaviorMembers =
        DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors;

    internal List<Assembly> AssembliesToScan { get; } = [];
    internal List<BehaviorRegistration> ClosedBehaviors { get; } = [];
    internal List<BehaviorRegistration> OpenBehaviors { get; } = [];
    internal List<BehaviorRegistration> ClosedStreamBehaviors { get; } = [];
    internal List<BehaviorRegistration> OpenStreamBehaviors { get; } = [];

    /// <summary>
    /// Lifetime used for handlers, behaviors and processors discovered by assembly scanning.
    /// Defaults to <see cref="ServiceLifetime.Transient"/>, matching MediatR.
    /// </summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    /// Lifetime of the mediator itself. Defaults to <see cref="ServiceLifetime.Scoped"/>.
    /// </summary>
    public ServiceLifetime MediatorLifetime { get; set; } = ServiceLifetime.Scoped;

    /// <summary>
    /// Execution mode for notification handlers. Defaults to
    /// <see cref="NotificationPublishMode.Parallel"/>.
    /// </summary>
    public NotificationPublishMode NotificationPublishMode { get; set; } = NotificationPublishMode.Parallel;

    /// <summary>
    /// The most notification handlers allowed to run at once when
    /// <see cref="NotificationPublishMode"/> is <see cref="NotificationPublishMode.BoundedParallel"/>.
    /// Required in that mode; ignored otherwise.
    /// </summary>
    public int? NotificationPublishMaxDegreeOfParallelism { get; set; }

    /// <summary>
    /// When two handlers claim the same request, throw instead of silently keeping the first one.
    /// Defaults to <see langword="true"/>: a duplicate handler is almost always a mistake, and
    /// discovering it as "my handler never runs" costs hours.
    /// </summary>
    public bool ThrowOnDuplicateRequestHandlers { get; set; } = true;

    /// <summary>Registers the assembly containing <typeparamref name="T"/> for scanning.</summary>
    public KyrolusMediatorConfiguration RegisterServicesFromAssemblyContaining<T>()
        => RegisterServicesFromAssembly(typeof(T).Assembly);

    /// <summary>Registers the assembly containing <paramref name="type"/> for scanning.</summary>
    public KyrolusMediatorConfiguration RegisterServicesFromAssemblyContaining(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return RegisterServicesFromAssembly(type.Assembly);
    }

    /// <summary>Registers an assembly to scan for handlers, behaviors and processors.</summary>
    public KyrolusMediatorConfiguration RegisterServicesFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (!AssembliesToScan.Contains(assembly))
            AssembliesToScan.Add(assembly);

        return this;
    }

    /// <summary>Registers several assemblies to scan.</summary>
    public KyrolusMediatorConfiguration RegisterServicesFromAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        if (assemblies.Length == 0)
            throw new ArgumentException("[KyrolusMediator] No assemblies were supplied.", nameof(assemblies));
        foreach (var assembly in assemblies)
            RegisterServicesFromAssembly(assembly);

        return this;
    }

    /// <summary>
    /// Adds a pipeline behavior closed over concrete request and response types.
    /// Behaviors run in the order they are added, unless they carry
    /// <see cref="PipelineOrderAttribute"/>.
    /// </summary>
    public KyrolusMediatorConfiguration AddBehavior<
        [DynamicallyAccessedMembers(BehaviorMembers)] TImplementation>()
        => AddBehavior(typeof(TImplementation));

    /// <inheritdoc cref="AddBehavior{TImplementation}()"/>
    public KyrolusMediatorConfiguration AddBehavior(
        [DynamicallyAccessedMembers(BehaviorMembers)] Type implementationType)
    {
        ArgumentNullException.ThrowIfNull(implementationType);

        var added = false;
        foreach (var iface in implementationType.GetInterfaces())
        {
            if (!iface.IsGenericType) continue;

            var definition = iface.GetGenericTypeDefinition();
            if (definition == typeof(IKyrolusPipelineBehavior<,>))
            {
                ClosedBehaviors.Add(new BehaviorRegistration(iface, implementationType));
                added = true;
            }
            else if (definition == typeof(IKyrolusStreamPipelineBehavior<,>))
            {
                ClosedStreamBehaviors.Add(new BehaviorRegistration(iface, implementationType));
                added = true;
            }
        }

        if (!added)
        {
            throw new ArgumentException(
                $"[KyrolusMediator] {implementationType.FullName} implements neither IKyrolusPipelineBehavior<,> nor IKyrolusStreamPipelineBehavior<,>.",
                nameof(implementationType));
        }

        return this;
    }

    /// <summary>
    /// Adds an open-generic pipeline behavior that applies to every request, such as
    /// <c>LoggingBehavior&lt;,&gt;</c>.
    /// </summary>
    public KyrolusMediatorConfiguration AddOpenBehavior(
        [DynamicallyAccessedMembers(BehaviorMembers)] Type openBehaviorType)
    {
        ArgumentNullException.ThrowIfNull(openBehaviorType);

        if (!openBehaviorType.IsGenericTypeDefinition)
        {
            throw new ArgumentException(
                $"[KyrolusMediator] {openBehaviorType.FullName} is not an open generic type. Use AddBehavior for closed types.",
                nameof(openBehaviorType));
        }

        var implemented = Array.ConvertAll(openBehaviorType.GetInterfaces(), i => i.IsGenericType ? i.GetGenericTypeDefinition() : i);

        if (Array.IndexOf(implemented, typeof(IKyrolusPipelineBehavior<,>)) >= 0)
        {
            OpenBehaviors.Add(new BehaviorRegistration(typeof(IKyrolusPipelineBehavior<,>), openBehaviorType));
            return this;
        }

        if (Array.IndexOf(implemented, typeof(IKyrolusStreamPipelineBehavior<,>)) >= 0)
        {
            OpenStreamBehaviors.Add(new BehaviorRegistration(typeof(IKyrolusStreamPipelineBehavior<,>), openBehaviorType));
            return this;
        }

        throw new ArgumentException(
            $"[KyrolusMediator] {openBehaviorType.FullName} implements neither IKyrolusPipelineBehavior<,> nor IKyrolusStreamPipelineBehavior<,>.",
            nameof(openBehaviorType));
    }

    /// <summary>
    /// Enables <c>kyrolus.mediator.*</c> metrics (request count and duration, via <see cref="System.Diagnostics.Metrics"/>)
    /// for every request dispatched through the pipeline. Equivalent to
    /// <c>AddOpenBehavior(typeof(KyrolusMediatorMetricsBehavior&lt;,&gt;))</c> - see that type for what
    /// gets recorded.
    /// </summary>
    public KyrolusMediatorConfiguration AddKyrolusMediatorMetrics()
        => AddOpenBehavior(typeof(KyrolusMediatorMetricsBehavior<,>));

    /// <summary>
    /// Logs the start, completion and failure of every request dispatched through the pipeline,
    /// via the standard <see cref="Microsoft.Extensions.Logging"/> API. Equivalent to
    /// <c>AddOpenBehavior(typeof(KyrolusMediatorLoggingBehavior&lt;,&gt;))</c> - see that type for
    /// what gets logged. Not registered by default.
    /// </summary>
    public KyrolusMediatorConfiguration AddKyrolusMediatorLogging()
        => AddOpenBehavior(typeof(KyrolusMediatorLoggingBehavior<,>));
}
