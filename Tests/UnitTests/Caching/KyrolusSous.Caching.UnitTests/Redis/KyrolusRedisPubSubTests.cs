namespace KyrolusSous.Caching.UnitTests.Redis;

public sealed class KyrolusRedisPubSubTests
{
    private sealed record ChatMessage(string Sender, string Content);

    [Fact(DisplayName = "KyrolusRedisPubSub: PublishAsync serializes and publishes message")]
    public async Task PubSub_PublishAsync_Success()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var subscriber = Substitute.For<ISubscriber>();
        muxer.GetSubscriber(Arg.Any<object>()).Returns(subscriber);

        var pubsub = new KyrolusRedisPubSub(muxer, new KyrolusJsonCacheSerializer());
        var msg = new ChatMessage("Kyrolus", "Hello PubSub");

        await pubsub.PublishAsync("chat-room", msg);

        await subscriber.Received(1).PublishAsync(
            Arg.Any<RedisChannel>(),
            Arg.Any<RedisValue>(),
            Arg.Any<CommandFlags>());
    }
}
