namespace KyrolusSous.Mediator.Tests;

/// <summary>Core dispatch, pipeline, notification and streaming behaviour.</summary>
public sealed class MediatorBehaviourTests
{
    private static ServiceProvider Build(Recorder recorder, Action<KyrolusMediatorConfiguration>? configure = null)
        => TestHost.Standard(recorder, configure).BuildServiceProvider();

    // --- Routing ---

    [Fact]
    public async Task Query_reaches_its_handler()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        (await mediator.SendAsync(new Ping("hi"))).ShouldBe("pong:hi");
    }

    [Fact]
    public async Task Command_with_response_reaches_its_handler()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        (await mediator.SendAsync(new CreateThing("x"))).ShouldBe(CreateThingHandler.KnownId);
    }

    [Fact]
    public async Task Command_without_response_reaches_its_handler()
    {
        var recorder = new Recorder();
        await using var provider = Build(recorder);
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        var id = Guid.NewGuid();

        await mediator.SendAsync(new DeleteThing(id));

        recorder.Entries.ShouldContain($"deleted:{id}");
    }

    [Fact]
    public async Task Query_sent_through_the_request_overload_still_routes_to_the_query_handler()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        IKyrolusRequest<string> asRequest = new Ping("via-request");

        (await mediator.SendAsync(asRequest)).ShouldBe("pong:via-request");
    }

    [Fact]
    public async Task Missing_handler_is_reported_with_the_request_name()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => mediator.SendAsync(new Unhandled()));

        exception.Message.ShouldContain(nameof(Unhandled));
    }

    [Fact]
    public async Task Null_request_is_rejected()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await Should.ThrowAsync<ArgumentNullException>(
            () => mediator.SendAsync((IKyrolusQuery<string>)null!));
    }

    // --- Untyped overloads ---

    [Fact]
    public async Task Untyped_send_discovers_the_response_type_from_the_request()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        object boxed = new Ping("untyped");

        (await mediator.SendAsync(boxed)).ShouldBe("pong:untyped");
    }

    [Fact]
    public async Task Untyped_send_rejects_an_object_that_is_not_a_request()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await Should.ThrowAsync<ArgumentException>(() => mediator.SendAsync(new object()));
    }

    [Fact]
    public async Task Untyped_publish_reaches_the_handlers()
    {
        var recorder = new Recorder();
        await using var provider = Build(recorder);
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        object boxed = new SomethingHappened("untyped");

        await mediator.PublishAsync(boxed);

        recorder.Entries.ShouldContain("first:untyped");
    }

    [Fact]
    public async Task Untyped_publish_rejects_an_object_that_is_not_a_notification()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await Should.ThrowAsync<ArgumentException>(() => mediator.PublishAsync(new object()));
    }

    // --- Pipeline ---

    [Fact]
    public async Task Behavior_can_short_circuit_without_calling_the_handler()
    {
        var recorder = new Recorder();
        await using var provider = Build(recorder, configuration =>
            configuration.AddBehavior<ShortCircuitBehavior>());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        (await mediator.SendAsync(new Ping("hi"))).ShouldBe("cached");
        recorder.Entries.ShouldNotContain("handler");
    }

    [Fact]
    public async Task Behavior_calling_next_without_a_token_still_reaches_the_handler()
    {
        var recorder = new Recorder();
        await using var provider = Build(recorder, configuration =>
            configuration.AddOpenBehavior(typeof(NoArgNextBehavior<,>)));
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        (await mediator.SendAsync(new Ping("hi"))).ShouldBe("pong:hi");
    }

    [Fact]
    public async Task Pre_and_post_processors_run_around_the_handler()
    {
        var recorder = new Recorder();
        await using var provider = TestHost.Standard(recorder).WithPingProcessors().BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await mediator.SendAsync(new Ping("hi"));

        var relevant = recorder.Entries.Where(e => e is "pre" or "handler" || e.StartsWith("post:")).ToArray();
        relevant.ShouldBe(["pre", "handler", "post:pong:hi"]);
    }

    [Fact]
    public async Task Exception_handler_can_supply_a_replacement_response()
    {
        var recorder = new Recorder();
        await using var provider = TestHost.Standard(recorder).WithExplodingQuery().BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        (await mediator.SendAsync(new Explode("recover"))).ShouldBe("recovered-response");
        recorder.Entries.ShouldContain("action:boom:recover");
    }

    [Fact]
    public async Task Unhandled_exception_is_rethrown_after_the_actions_run()
    {
        var recorder = new Recorder();
        await using var provider = TestHost.Standard(recorder).WithExplodingQuery().BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await Should.ThrowAsync<InvalidOperationException>(() => mediator.SendAsync(new Explode("rethrow")));
        recorder.Entries.ShouldContain("action:boom:rethrow");
    }

    // --- Notifications ---

    [Fact]
    public async Task Notification_reaches_every_registered_handler()
    {
        var recorder = new Recorder();
        await using var provider = Build(recorder);
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await mediator.PublishAsync(new SomethingHappened("x"));

        recorder.Entries.ShouldContain("first:x");
        recorder.Entries.ShouldContain("second:x");
    }

    [Fact]
    public async Task Publishing_a_notification_with_no_handlers_is_a_no_op()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator();
        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await Should.NotThrowAsync(() => mediator.PublishAsync(new SomethingHappened("nobody-listening")));
    }

    [Fact]
    public async Task One_failing_handler_does_not_stop_the_others()
    {
        var recorder = new Recorder();
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddKyrolusMediator();
        services.AddTransient<INotificationHandler<SomethingHappened>, ThrowingNotificationHandler>();
        services.AddTransient<INotificationHandler<SomethingHappened>, RecordingNotificationHandler>();

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IKyrolusMediatorPublisher>();

        await Should.ThrowAsync<AggregateException>(() => publisher.PublishAsync(new SomethingHappened("x")));
        recorder.Entries.ShouldContain("first:x");
    }

    [Fact]
    public async Task Sequential_mode_runs_handlers_one_at_a_time_in_registration_order()
    {
        var recorder = new Recorder();
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddKyrolusMediator(configuration =>
            configuration.NotificationPublishMode = NotificationPublishMode.Sequential);
        services.AddTransient<INotificationHandler<SomethingHappened>, RecordingNotificationHandler>();
        services.AddTransient<INotificationHandler<SomethingHappened>, SecondRecordingNotificationHandler>();

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IKyrolusMediatorPublisher>();

        await publisher.PublishAsync(new SomethingHappened("x"));

        recorder.Entries.ShouldBe(["first:x", "second:x"]);
    }

    [Fact]
    public async Task Per_call_strategy_overrides_the_configured_one()
    {
        var recorder = new Recorder();
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddKyrolusMediator();
        services.AddTransient<INotificationHandler<SomethingHappened>, RecordingNotificationHandler>();
        services.AddTransient<INotificationHandler<SomethingHappened>, SecondRecordingNotificationHandler>();

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IKyrolusMediatorPublisher>();

        await publisher.PublishAsync(
            new SomethingHappened("x"),
            new KyrolusSous.Mediator.Runtime.Implementations.KyrolusSequentialNotificationPublishStrategy());

        recorder.Entries.ShouldBe(["first:x", "second:x"]);
    }

    // --- Streaming ---

    [Fact]
    public async Task Stream_request_yields_every_item()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        var items = new List<int>();
        await foreach (var item in mediator.StreamAsync(new CountTo(4)))
        {
            items.Add(item);
        }

        items.ShouldBe([1, 2, 3, 4]);
    }

    [Fact]
    public async Task Untyped_stream_yields_boxed_items()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        object boxed = new CountTo(3);

        var items = new List<object?>();
        await foreach (var item in mediator.StreamAsync(boxed))
        {
            items.Add(item);
        }

        items.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Cancelling_a_stream_stops_it()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        using var cts = new CancellationTokenSource();

        var seen = 0;
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in mediator.StreamAsync(new CountTo(1000), cts.Token))
            {
                seen++;
                if (seen == 3)
                {
                    await cts.CancelAsync();
                }
            }
        });

        seen.ShouldBe(3);
    }
}

/// <summary>A request with no handler registered anywhere.</summary>
public sealed record Unhandled : IKyrolusQuery<string>;
