namespace KyrolusSous.Mediator.Tests;

/// <summary>
/// One test per defect found in the review. Each fails against the pre-fix code.
/// </summary>
public sealed class RegressionTests
{
    private static ServiceProvider Build(Recorder recorder, Action<KyrolusMediatorConfiguration>? configure = null)
        => TestHost.Standard(recorder, configure).BuildServiceProvider();

    /// <summary>
    /// The Handle-method cache used to be keyed on the handler type alone, so a class handling
    /// two notifications reused the first notification's Handle for the second.
    /// </summary>
    [Fact]
    public async Task Publisher_routes_each_notification_to_its_own_handle_overload()
    {
        var recorder = new Recorder();
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddKyrolusMediator();
        services.AddTransient<INotificationHandler<SomethingHappened>, DualNotificationHandler>();
        services.AddTransient<INotificationHandler<SomethingElseHappened>, DualNotificationHandler>();

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IKyrolusMediatorPublisher>();

        // Order matters: the first publish is what poisoned the cache.
        await publisher.PublishAsync(new SomethingHappened("a"));
        await publisher.PublishAsync(new SomethingElseHappened("b"));

        recorder.Entries.ShouldBe(["happened:a", "else:b"]);
    }

    /// <summary>Same cache-key defect, on the request dispatch path.</summary>
    [Fact]
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

    /// <summary>
    /// Exceptions were collected into a plain List while the default strategy runs handlers in
    /// parallel, so a concurrent failure could lose an entry or tear the backing array.
    /// </summary>
    [Fact]
    public async Task Publisher_collects_every_exception_when_handlers_fail_in_parallel()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator();
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

    /// <summary>Repeats the parallel-failure publish to catch a race that only shows intermittently.</summary>
    [Fact]
    public async Task Publisher_exception_collection_is_stable_under_repetition()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator();
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

    /// <summary>
    /// Behaviors without <see cref="PipelineOrderAttribute"/> all share order 0. List.Sort is
    /// introsort and unstable, so their relative order used to be undefined; OrderBy is stable
    /// and preserves DI registration order.
    /// </summary>
    [Fact]
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

    /// <summary>The attribute still wins over registration order.</summary>
    [Fact]
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

    /// <summary>
    /// Two handlers claiming one request used to leave the first silently winning. That is
    /// nearly always a mistake, so scanning now reports it.
    /// </summary>
    [Fact]
    public void Duplicate_request_handlers_are_reported()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Recorder());

        var exception = Should.Throw<InvalidOperationException>(() =>
            services.AddKyrolusMediator(configuration =>
                configuration.RegisterServicesFromAssemblyContaining<Ambiguous>()));

        exception.Message.ShouldContain(nameof(AmbiguousHandlerOne));
        exception.Message.ShouldContain(nameof(AmbiguousHandlerTwo));
    }

    /// <summary>Opting out keeps the previous first-wins behaviour.</summary>
    [Fact]
    public void Duplicate_request_handlers_can_be_tolerated_explicitly()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Recorder());

        Should.NotThrow(() => services.AddKyrolusMediator(configuration =>
        {
            configuration.RegisterServicesFromAssemblyContaining<Ambiguous>();
            configuration.ThrowOnDuplicateRequestHandlers = false;
        }));
    }

    /// <summary>
    /// The strategy toggles used to stack registrations, leaving resolution dependent on order.
    /// </summary>
    [Fact]
    public void Notification_strategy_toggle_replaces_rather_than_stacks()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator();
        services.UseKyrolusMediatorSequentialNotifications();
        services.UseKyrolusMediatorParallelNotifications();
        services.UseKyrolusMediatorSequentialNotifications();

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IKyrolusNotificationPublishStrategy>().Count().ShouldBe(1);
        provider.GetRequiredService<IKyrolusNotificationPublishStrategy>()
            .ShouldBeOfType<KyrolusSous.Mediator.Runtime.Implementations.KyrolusSequentialNotificationPublishStrategy>();
    }
}
