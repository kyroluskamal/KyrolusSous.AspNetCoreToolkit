namespace KyrolusSous.Caching.UnitTests.Redis;

public sealed class KyrolusRedisInvalidationBusTests
{
    [Fact(DisplayName = "KyrolusRedisInvalidationBus: PublishAsync should encode message and publish to subscriber")]
    public async Task PublishAsync_EncodesAndPublishes()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var subscriber = Substitute.For<ISubscriber>();
        muxer.GetSubscriber(Arg.Any<object>()).Returns(subscriber);

        var bus = new KyrolusRedisInvalidationBus(muxer);
        var message = new KyrolusCacheInvalidationMessage(KyrolusCacheInvalidationKind.Key, ["user:101"]);

        await bus.PublishAsync(message);

        await subscriber.Received(1).PublishAsync(
            Arg.Any<RedisChannel>(),
            Arg.Is<RedisValue>(v => v.ToString().StartsWith("1:")), // Kind 1 (Key) + Base64
            Arg.Any<CommandFlags>());
    }

    [Fact(DisplayName = "KyrolusRedisInvalidationBus: When Publish is disabled, PublishAsync should no-op")]
    public async Task PublishAsync_Disabled_NoOps()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var subscriber = Substitute.For<ISubscriber>();
        muxer.GetSubscriber(Arg.Any<object>()).Returns(subscriber);

        var bus = new KyrolusRedisInvalidationBus(muxer, new KyrolusRedisInvalidationOptions { Publish = false });
        await bus.PublishAsync(new KyrolusCacheInvalidationMessage(KyrolusCacheInvalidationKind.Key, ["user:1"]));

        await subscriber.DidNotReceive().PublishAsync(Arg.Any<RedisChannel>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>());
    }

    [Fact(DisplayName = "KyrolusRedisInvalidationBus: When Subscribe is disabled, Subscribe should return noop subscription")]
    public void Subscribe_WhenDisabled_ReturnsNoopSubscription()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var bus = new KyrolusRedisInvalidationBus(muxer, new KyrolusRedisInvalidationOptions { Subscribe = false });

        var subscription = bus.Subscribe(_ => Task.CompletedTask);

        subscription.ShouldNotBeNull();
        subscription.Dispose(); // Should not throw
    }

    [Fact(DisplayName = "KyrolusRedisInvalidationBus: PublishAsync should throw when cancellation is requested")]
    public async Task PublishAsync_Cancelled_Throws()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var bus = new KyrolusRedisInvalidationBus(muxer);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await bus.PublishAsync(new KyrolusCacheInvalidationMessage(KyrolusCacheInvalidationKind.Key, ["user:1"]), cts.Token));
    }
}
