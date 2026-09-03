namespace KyrolusSous.Mediator.Runtime.UnitTests;

public sealed class KyrolusBoundedParallelNotificationPublishStrategyTests
{
    [Theory(DisplayName = "Constructor throws ArgumentOutOfRangeException when maxDegreeOfParallelism is less than 1")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_throws_when_cap_is_not_positive(int maxDegreeOfParallelism)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new Implementations.KyrolusBoundedParallelNotificationPublishStrategy(maxDegreeOfParallelism));

        exception.ParamName.ShouldBe("maxDegreeOfParallelism");
    }

    [Fact(DisplayName = "Bounded parallel strategy never runs more handlers at once than the configured cap")]
    public async Task Bounded_strategy_caps_concurrency()
    {
        const int cap = 2;
        var strategy = new Implementations.KyrolusBoundedParallelNotificationPublishStrategy(cap);

        var currentlyRunning = 0;
        var maxObserved = 0;
        var gate = new object();

        var handlers = Enumerable.Range(0, 6)
            .Select(_ => (Func<CancellationToken, Task>)(async ct =>
            {
                lock (gate)
                {
                    currentlyRunning++;
                    maxObserved = Math.Max(maxObserved, currentlyRunning);
                }

                await Task.Delay(50, ct);

                lock (gate) { currentlyRunning--; }
            }))
            .ToList();

        await strategy.PublishAsync(handlers, CancellationToken.None);

        // <= cap is guaranteed by the semaphore regardless of timing; == cap is what proves this
        // is actually running handlers in parallel up to the cap rather than one at a time.
        maxObserved.ShouldBe(cap);
    }

    [Fact(DisplayName = "Bounded parallel strategy runs every handler exactly once")]
    public async Task Bounded_strategy_runs_every_handler()
    {
        var strategy = new Implementations.KyrolusBoundedParallelNotificationPublishStrategy(3);
        var calls = 0;

        var handlers = Enumerable.Range(0, 10)
            .Select(_ => (Func<CancellationToken, Task>)(_ =>
            {
                Interlocked.Increment(ref calls);
                return Task.CompletedTask;
            }))
            .ToList();

        await strategy.PublishAsync(handlers, CancellationToken.None);

        calls.ShouldBe(10);
    }

    [Fact(DisplayName = "UseKyrolusMediatorBoundedParallelNotifications wires the strategy end to end through the publisher")]
    public async Task Wired_through_publisher()
    {
        var recorder = new Recorder();
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddKyrolusMediator();
        services.AddKyrolusMediatorReflection();
        services.UseKyrolusMediatorBoundedParallelNotifications(4);
        services.AddTransient<INotificationHandler<SomethingHappened>, RecordingNotificationHandler>();
        services.AddTransient<INotificationHandler<SomethingHappened>, SecondRecordingNotificationHandler>();

        await using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IKyrolusNotificationPublishStrategy>()
            .ShouldBeOfType<Implementations.KyrolusBoundedParallelNotificationPublishStrategy>();

        var publisher = provider.GetRequiredService<IKyrolusMediatorPublisher>();
        await publisher.PublishAsync(new SomethingHappened("x"));

        recorder.Entries.OrderBy(e => e).ShouldBe(["first:x", "second:x"]);
    }

    [Fact(DisplayName = "UseKyrolusMediatorBoundedParallelNotifications throws ArgumentOutOfRangeException when the cap is not positive")]
    public void UseKyrolusMediatorBoundedParallelNotifications_throws_when_cap_is_not_positive()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => services.UseKyrolusMediatorBoundedParallelNotifications(0));

        exception.ParamName.ShouldBe("maxDegreeOfParallelism");
    }
}
