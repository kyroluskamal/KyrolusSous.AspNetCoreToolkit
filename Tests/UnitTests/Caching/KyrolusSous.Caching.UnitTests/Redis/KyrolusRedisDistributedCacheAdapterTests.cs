namespace KyrolusSous.Caching.UnitTests.Redis;

public sealed class KyrolusRedisDistributedCacheAdapterTests
{
    [Fact(DisplayName = "KyrolusRedisDistributedCacheAdapter: Implements IDistributedCache methods correctly")]
    public async Task DistributedCacheAdapter_GetAndSet()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        muxer.GetDatabase().Returns(db);

        var payload = Encoding.UTF8.GetBytes("SessionData");
        db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(Task.FromResult((RedisValue)payload));

        var adapter = new KyrolusRedisDistributedCacheAdapter(muxer);

        // GetAsync
        var result = await adapter.GetAsync("session:123");
        result.ShouldBe(payload);

        // SetAsync
        await adapter.SetAsync("session:123", payload, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        });

        await db.Received().StringSetAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Any<Expiration>(),
            Arg.Any<ValueCondition>(),
            Arg.Any<CommandFlags>());

        // RemoveAsync
        await adapter.RemoveAsync("session:123");
        await db.Received().KeyDeleteAsync(Arg.Any<RedisKey[]>(), Arg.Any<CommandFlags>());
    }
}
