namespace KyrolusSous.Mediator.Runtime.UnitTests;

public sealed class KyrolusMediatorPublisherTests
{
    private static ServiceProvider Build(Recorder recorder, Action<KyrolusMediatorConfiguration>? configure = null)
        => TestHost.Standard(recorder, configure).BuildServiceProvider();

    [Fact(DisplayName = "PublishAsync dispatches notification to every registered notification handler")]
    public async Task Notification_reaches_every_registered_handler()
    {
        var recorder = new Recorder();
        await using var provider = Build(recorder);
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await mediator.PublishAsync(new SomethingHappened("x"));

        recorder.Entries.ShouldContain("first:x");
        recorder.Entries.ShouldContain("second:x");
    }

    [Fact(DisplayName = "PublishAsync with no registered handlers is a safe no-op")]
    public async Task Publishing_a_notification_with_no_handlers_is_a_no_op()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediator();
        services.AddKyrolusMediatorReflection();
        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await Should.NotThrowAsync(() => mediator.PublishAsync(new SomethingHappened("nobody-listening")));
    }

    [Fact(DisplayName = "PublishAsync continues executing remaining handlers when one handler throws, then rethrows AggregateException")]
    public async Task One_failing_handler_does_not_stop_the_others()
    {
        var recorder = new Recorder();
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddKyrolusMediator();
        services.AddKyrolusMediatorReflection();
        services.AddTransient<INotificationHandler<SomethingHappened>, ThrowingNotificationHandler>();
        services.AddTransient<INotificationHandler<SomethingHappened>, RecordingNotificationHandler>();

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IKyrolusMediatorPublisher>();

        await Should.ThrowAsync<AggregateException>(() => publisher.PublishAsync(new SomethingHappened("x")));
        recorder.Entries.ShouldContain("first:x");
    }

    [Fact(DisplayName = "Untyped PublishAsync dispatches notification object to registered handlers")]
    public async Task Untyped_publish_reaches_the_handlers()
    {
        var recorder = new Recorder();
        await using var provider = Build(recorder);
        var mediator = provider.GetRequiredService<IKyrolusMediator>();
        object boxed = new SomethingHappened("untyped");

        await mediator.PublishAsync(boxed);

        recorder.Entries.ShouldContain("first:untyped");
    }

    [Fact(DisplayName = "Untyped PublishAsync throws ArgumentException when object does not implement INotification")]
    public async Task Untyped_publish_rejects_an_object_that_is_not_a_notification()
    {
        await using var provider = Build(new Recorder());
        var mediator = provider.GetRequiredService<IKyrolusMediator>();

        await Should.ThrowAsync<ArgumentException>(() => mediator.PublishAsync(new object()));
    }
}
