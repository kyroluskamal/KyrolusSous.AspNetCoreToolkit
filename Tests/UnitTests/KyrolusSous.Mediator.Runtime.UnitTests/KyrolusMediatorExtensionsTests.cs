namespace KyrolusSous.Mediator.Runtime.UnitTests;

public sealed class KyrolusMediatorExtensionsTests
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

        services.AddKyrolusMediatorReflection();
        return services;
    }

    [Fact(DisplayName = "Scanning assembly finds and registers request handlers in ServiceCollection")]
    public void Scanning_finds_request_handlers()
    {
        using var provider = Scanned().BuildServiceProvider();

        provider.GetService<IKyrolusQueryHandler<Ping, string>>().ShouldBeOfType<PingHandler>();
        provider.GetService<IKyrolusCommandHandler<DeleteThing>>().ShouldBeOfType<DeleteThingHandler>();
    }

    [Fact(DisplayName = "Scanning assembly finds all registered notification handlers for a notification")]
    public void Scanning_finds_every_notification_handler_for_a_notification()
    {
        using var provider = Scanned().BuildServiceProvider();

        var handlers = provider.GetServices<INotificationHandler<SomethingHappened>>().ToArray();

        handlers.ShouldContain(h => h is RecordingNotificationHandler);
        handlers.ShouldContain(h => h is SecondRecordingNotificationHandler);
    }

    [Fact(DisplayName = "Scanning assembly finds and registers pre and post request processors")]
    public void Scanning_finds_pre_and_post_processors()
    {
        using var provider = Scanned().BuildServiceProvider();

        provider.GetServices<IKyrolusRequestPreProcessor<Ping>>().ShouldContain(p => p is PingPreProcessor);
        provider.GetServices<IKyrolusRequestPostProcessor<Ping, string>>().ShouldContain(p => p is PingPostProcessor);
    }

    [Fact(DisplayName = "Scanning assembly finds and registers stream request handlers")]
    public void Scanning_finds_stream_handlers()
    {
        using var provider = Scanned().BuildServiceProvider();

        provider.GetService<IKyrolusStreamRequestHandler<CountTo, int>>().ShouldBeOfType<CountToHandler>();
    }

    [Fact(DisplayName = "AddKyrolusMediatorFromAssemblies requires at least one assembly specified")]
    public void Scanning_requires_at_least_one_assembly()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddKyrolusMediatorFromAssemblies());
    }

    [Fact(DisplayName = "Built-in behaviors are registered idempotently once across multiple AddKyrolusMediator calls")]
    public void Built_in_behaviors_are_registered_once_even_across_repeated_calls()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator();
        services.AddKyrolusMediatorReflection();
        services.AddKyrolusMediator();
        services.AddKyrolusMediatorReflection();

        var exceptionBehaviors = services.Count(d =>
            d.ImplementationType == typeof(Implementations.KyrolusRequestExceptionProcessorBehavior<,>));

        exceptionBehaviors.ShouldBe(1);
    }

    [Fact(DisplayName = "Compatibility IMediator interface resolves to KyrolusMediator implementation")]
    public void Compatibility_mediator_resolves_to_the_same_implementation()
    {
        using var provider = Scanned().BuildServiceProvider();

        provider.GetRequiredService<Abstractions.Compatibility.IMediator>()
            .ShouldBeOfType<Implementations.KyrolusMediator>();
    }
}
