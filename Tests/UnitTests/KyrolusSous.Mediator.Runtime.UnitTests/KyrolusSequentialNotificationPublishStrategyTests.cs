namespace KyrolusSous.Mediator.Runtime.UnitTests;

public sealed class KyrolusSequentialNotificationPublishStrategyTests
{
    [Fact(DisplayName = "Sequential publish strategy executes notification handlers sequentially in registration order")]
    public async Task Sequential_mode_runs_handlers_one_at_a_time_in_registration_order()
    {
        var recorder = new Recorder();
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddKyrolusMediator(configuration =>
            configuration.NotificationPublishMode = NotificationPublishMode.Sequential);
        services.AddKyrolusMediatorReflection();
        services.AddTransient<INotificationHandler<SomethingHappened>, RecordingNotificationHandler>();
        services.AddTransient<INotificationHandler<SomethingHappened>, SecondRecordingNotificationHandler>();

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IKyrolusMediatorPublisher>();

        await publisher.PublishAsync(new SomethingHappened("x"));

        recorder.Entries.ShouldBe(["first:x", "second:x"]);
    }

    [Fact(DisplayName = "Per-call sequential strategy instance overrides configured default publish strategy")]
    public async Task Per_call_strategy_overrides_the_configured_one()
    {
        var recorder = new Recorder();
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddKyrolusMediator();
        services.AddKyrolusMediatorReflection();
        services.AddTransient<INotificationHandler<SomethingHappened>, RecordingNotificationHandler>();
        services.AddTransient<INotificationHandler<SomethingHappened>, SecondRecordingNotificationHandler>();

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IKyrolusMediatorPublisher>();

        await publisher.PublishAsync(
            new SomethingHappened("x"),
            new Implementations.KyrolusSequentialNotificationPublishStrategy());

        recorder.Entries.ShouldBe(["first:x", "second:x"]);
    }
}
