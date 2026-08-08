
namespace KyrolusSous.Mediator.Runtime.UnitTests;

/// <summary>
/// Assembly scanning and <see cref="KyrolusMediatorConfiguration"/>. These deliberately scan the
/// test assembly - including its adversarial probes - so they assert only on what scanning
/// itself guarantees.
/// </summary>
public sealed class ScanningTests
{
    private static IServiceCollection Scanned(Action<KyrolusMediatorConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Recorder());
        services.AddKyrolusMediator(configuration =>
        {
            configuration.RegisterServicesFromAssemblyContaining<Ping>();
            configuration.ThrowOnDuplicateRequestHandlers = false;
            configure?.Invoke(configuration);
        });

        // Scanning lives in the reflection package - it is the thing the generator replaces - so
        // this is what actually walks the assembly.
        services.AddKyrolusMediatorReflection();

        return services;
    }

    [Fact]
    public void Scanning_finds_request_handlers()
    {
        using var provider = Scanned().BuildServiceProvider();

        provider.GetService<IKyrolusQueryHandler<Ping, string>>().ShouldBeOfType<PingHandler>();
        provider.GetService<IKyrolusCommandHandler<DeleteThing>>().ShouldBeOfType<DeleteThingHandler>();
    }

    [Fact]
    public void Scanning_finds_every_notification_handler_for_a_notification()
    {
        using var provider = Scanned().BuildServiceProvider();

        var handlers = provider.GetServices<INotificationHandler<SomethingHappened>>().ToArray();

        handlers.ShouldContain(h => h is RecordingNotificationHandler);
        handlers.ShouldContain(h => h is SecondRecordingNotificationHandler);
    }

    [Fact]
    public void Scanning_finds_pre_and_post_processors()
    {
        using var provider = Scanned().BuildServiceProvider();

        provider.GetServices<IKyrolusRequestPreProcessor<Ping>>().ShouldContain(p => p is PingPreProcessor);
        provider.GetServices<IKyrolusRequestPostProcessor<Ping, string>>().ShouldContain(p => p is PingPostProcessor);
    }

    [Fact]
    public void Scanning_finds_stream_handlers()
    {
        using var provider = Scanned().BuildServiceProvider();

        provider.GetService<IKyrolusStreamRequestHandler<CountTo, int>>().ShouldBeOfType<CountToHandler>();
    }

    [Fact]
    public void Scanning_requires_at_least_one_assembly()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddKyrolusMediatorFromAssemblies());
    }

    [Fact]
    public void Registering_the_same_assembly_twice_is_idempotent()
    {
        var configuration = new KyrolusMediatorConfiguration();

        configuration.RegisterServicesFromAssemblyContaining<Ping>();
        configuration.RegisterServicesFromAssemblyContaining<Ambiguous>(); // same assembly

        configuration.AssembliesToScan.Count.ShouldBe(1);
    }

    // --- Lifetimes ---

    [Fact]
    public void Handler_lifetime_is_configurable()
    {
        var services = Scanned(configuration => configuration.Lifetime = ServiceLifetime.Scoped);

        var descriptor = services.First(d => d.ServiceType == typeof(IKyrolusQueryHandler<Ping, string>));

        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Handler_lifetime_defaults_to_transient()
    {
        var services = Scanned();

        var descriptor = services.First(d => d.ServiceType == typeof(IKyrolusQueryHandler<Ping, string>));

        descriptor.Lifetime.ShouldBe(ServiceLifetime.Transient);
    }

    [Fact]
    public void Mediator_lifetime_is_configurable()
    {
        var services = Scanned(configuration => configuration.MediatorLifetime = ServiceLifetime.Singleton);

        var descriptor = services.First(d => d.ServiceType == typeof(IKyrolusMediator));

        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    // --- Behavior registration ---

    [Fact]
    public void AddOpenBehavior_rejects_a_closed_type()
        => Should.Throw<ArgumentException>(() => new KyrolusMediatorConfiguration()
            .AddOpenBehavior(typeof(ShortCircuitBehavior)));

    [Fact]
    public void AddOpenBehavior_rejects_a_type_that_is_not_a_behavior()
        => Should.Throw<ArgumentException>(() => new KyrolusMediatorConfiguration()
            .AddOpenBehavior(typeof(List<>)));

    [Fact]
    public void AddBehavior_rejects_a_type_that_is_not_a_behavior()
        => Should.Throw<ArgumentException>(() => new KyrolusMediatorConfiguration()
            .AddBehavior<ScanningTests>());

    [Fact]
    public void AddBehavior_registers_the_closed_interface()
    {
        var configuration = new KyrolusMediatorConfiguration();

        configuration.AddBehavior<ShortCircuitBehavior>();

        configuration.ClosedBehaviors.ShouldHaveSingleItem();
        configuration.ClosedBehaviors[0].Service.ShouldBe(typeof(IKyrolusPipelineBehavior<Ping, string>));
    }

    [Fact]
    public void Built_in_behaviors_are_registered_once_even_across_repeated_calls()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator();
        services.AddKyrolusMediatorReflection();
        services.AddKyrolusMediator();
        services.AddKyrolusMediatorReflection();

        var exceptionBehaviors = services.Count(d =>
            d.ImplementationType == typeof(KyrolusSous.Mediator.Runtime.Implementations.KyrolusRequestExceptionProcessorBehavior<,>));

        exceptionBehaviors.ShouldBe(1);
    }

    // --- MediatR compatibility surface ---

    [Fact]
    public void Compatibility_mediator_resolves_to_the_same_implementation()
    {
        using var provider = Scanned().BuildServiceProvider();

        provider.GetRequiredService<KyrolusSous.Mediator.Abstractions.Compatibility.IMediator>()
            .ShouldBeOfType<KyrolusSous.Mediator.Runtime.Implementations.KyrolusMediator>();
    }

    /// <summary>
    /// A behavior written the MediatR way - open over any request, no response constraint -
    /// must compile and run. The old compat constraint rejected exactly this shape.
    /// </summary>
    [Fact]
    public async Task MediatR_style_open_behavior_runs()
    {
        var recorder = new Recorder();
        var services = TestHost.Standard(recorder, configuration =>
            configuration.AddOpenBehavior(typeof(MediatRStyleBehavior<,>)));

        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await mediator.SendAsync(new Ping("hi"));

        recorder.Entries.ShouldContain("mediatr-style");
    }
}

/// <summary>Declared against the compatibility interface, with MediatR's own constraints.</summary>
public sealed class MediatRStyleBehavior<TRequest, TResponse>(Recorder recorder)
    : KyrolusSous.Mediator.Abstractions.Compatibility.IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        recorder.Add("mediatr-style");
        return next(cancellationToken);
    }
}
