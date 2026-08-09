namespace KyrolusSous.Mediator.Runtime.UnitTests;

/// <summary>
/// One test per defect found in the review. Each fails against the pre-fix code.
/// </summary>
public sealed class KyrolusMediatorRegressionTests
{
    private static ServiceProvider Build(Recorder recorder, Action<KyrolusMediatorConfiguration>? configure = null)
        => TestHost.Standard(recorder, configure).BuildServiceProvider();

    [Fact(DisplayName = "Publisher routes each notification type to its own Handle method overload without caching collisions")]
    public async Task Publisher_routes_each_notification_to_its_own_handle_overload()
    {
        var recorder = new Recorder();
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddKyrolusMediator();
        services.AddKyrolusMediatorReflection();
        services.AddTransient<INotificationHandler<SomethingHappened>, DualNotificationHandler>();
        services.AddTransient<INotificationHandler<SomethingElseHappened>, DualNotificationHandler>();

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IKyrolusMediatorPublisher>();

        // Order matters: the first publish is what poisoned the cache in past versions.
        await publisher.PublishAsync(new SomethingHappened("a"));
        await publisher.PublishAsync(new SomethingElseHappened("b"));

        recorder.Entries.ShouldBe(["happened:a", "else:b"]);
    }

    [Fact(DisplayName = "Dispatcher routes each request type to its own Handle method overload without caching collisions")]
    public async Task Dispatcher_routes_each_request_to_its_own_handle_overload()
    {
        var recorder = new Recorder();
        await using var provider = Build(recorder);
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        var first = await mediator.SendAsync(new FirstRequest(1));
        var second = await mediator.SendAsync(new SecondRequest(2));

        first.ShouldBe("first:1");
        second.ShouldBe("second:2");
    }

    [Fact(DisplayName = "Publisher thread-safely collects all exceptions when handlers fail concurrently in parallel mode")]
    public async Task Publisher_collects_every_exception_when_handlers_fail_in_parallel()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator();
        services.AddKyrolusMediatorReflection();
        services.AddTransient<INotificationHandler<SomethingHappened>, ThrowingNotificationHandler>();
        services.AddTransient<INotificationHandler<SomethingHappened>, SecondThrowingNotificationHandler>();

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IKyrolusMediatorPublisher>();

        var aggregate = await Should.ThrowAsync<AggregateException>(
            () => publisher.PublishAsync(new SomethingHappened("x")));

        aggregate.InnerExceptions.Count.ShouldBe(2);
        aggregate.InnerExceptions.Select(e => e.Message)
            .OrderBy(m => m)
            .ShouldBe(["handler-one-failed", "handler-two-failed"]);
    }

    [Fact(DisplayName = "Publisher exception collection stability under high repetition count")]
    public async Task Publisher_exception_collection_is_stable_under_repetition()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator();
        services.AddKyrolusMediatorReflection();
        services.AddTransient<INotificationHandler<SomethingHappened>, ThrowingNotificationHandler>();
        services.AddTransient<INotificationHandler<SomethingHappened>, SecondThrowingNotificationHandler>();

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IKyrolusMediatorPublisher>();

        for (var i = 0; i < 200; i++)
        {
            var aggregate = await Should.ThrowAsync<AggregateException>(
                () => publisher.PublishAsync(new SomethingHappened($"x{i}")));

            aggregate.InnerExceptions.Count.ShouldBe(2);
        }
    }

    [Fact(DisplayName = "Behaviors without explicitly defined PipelineOrder attribute execute in registration order")]
    public async Task Behaviors_without_an_order_run_in_registration_order()
    {
        var recorder = new Recorder();
        await using var provider = Build(recorder, configuration =>
        {
            configuration.AddOpenBehavior(typeof(UnorderedBehaviorA<,>));
            configuration.AddOpenBehavior(typeof(UnorderedBehaviorB<,>));
            configuration.AddOpenBehavior(typeof(UnorderedBehaviorC<,>));
        });
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await mediator.SendAsync(new Ping("hi"));

        var ordered = recorder.Entries.Where(e => e is "A" or "B" or "C").ToArray();
        ordered.ShouldBe(["A", "B", "C"]);
    }

    [Fact(DisplayName = "PipelineOrder attribute value overrides service registration order in behavior pipeline")]
    public async Task PipelineOrder_attribute_overrides_registration_order()
    {
        var recorder = new Recorder();
        await using var provider = Build(recorder, configuration =>
        {
            configuration.AddOpenBehavior(typeof(LateBehavior<,>));   // order +50, registered first
            configuration.AddOpenBehavior(typeof(EarlyBehavior<,>));  // order -50, registered second
        });
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await mediator.SendAsync(new Ping("hi"));

        var ordered = recorder.Entries.Where(e => e is "early" or "late").ToArray();
        ordered.ShouldBe(["early", "late"]);
    }

    [Fact(DisplayName = "Duplicate request handlers detection throws InvalidOperationException during assembly scanning")]
    public void Duplicate_request_handlers_are_reported()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Recorder());

        services.AddKyrolusMediator(configuration =>
            configuration.RegisterServicesFromAssemblyContaining<Ambiguous>());

        var exception = Should.Throw<InvalidOperationException>(services.AddKyrolusMediatorReflection);

        exception.Message.ShouldContain(nameof(AmbiguousHandlerOne));
        exception.Message.ShouldContain(nameof(AmbiguousHandlerTwo));
    }

    [Fact(DisplayName = "Duplicate request handlers can be explicitly tolerated via ThrowOnDuplicateRequestHandlers = false")]
    public void Duplicate_request_handlers_can_be_tolerated_explicitly()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Recorder());

        services.AddKyrolusMediator(configuration =>
        {
            configuration.RegisterServicesFromAssemblyContaining<Ambiguous>();
            configuration.ThrowOnDuplicateRequestHandlers = false;
        });

        Should.NotThrow(services.AddKyrolusMediatorReflection);
    }

    [Fact(DisplayName = "Notification strategy toggles replace single registration rather than stacking multiple instances")]
    public void Notification_strategy_toggle_replaces_rather_than_stacks()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator();
        services.AddKyrolusMediatorReflection();
        services.UseKyrolusMediatorSequentialNotifications();
        services.UseKyrolusMediatorParallelNotifications();
        services.UseKyrolusMediatorSequentialNotifications();

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IKyrolusNotificationPublishStrategy>().Count().ShouldBe(1);
        provider.GetRequiredService<IKyrolusNotificationPublishStrategy>()
            .ShouldBeOfType<Implementations.KyrolusSequentialNotificationPublishStrategy>();
    }
}
