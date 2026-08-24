namespace KyrolusSous.Caching.UnitTests.Redis;

public sealed class KyrolusRedisOutputCacheStoreTests
{
    [Fact(DisplayName = "KyrolusRedisOutputCacheStore: Implements IOutputCacheStore methods")]
    public async Task OutputCacheStore_GetSetEvict()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        muxer.GetDatabase().Returns(db);

        var body = Encoding.UTF8.GetBytes("<html><body>Hello World</body></html>");
        db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(Task.FromResult((RedisValue)body));

        var store = new KyrolusRedisOutputCacheStore(muxer);

        // GetAsync
        var cached = await store.GetAsync("page:/home", CancellationToken.None);
        cached.ShouldBe(body);

        // SetAsync with tags
        await store.SetAsync("page:/home", body, ["home", "public"], TimeSpan.FromMinutes(5), CancellationToken.None);
        await db.Received().StringSetAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Any<Expiration>(),
            Arg.Any<ValueCondition>(),
            Arg.Any<CommandFlags>());

        // EvictByTagAsync executes Lua script
        await store.EvictByTagAsync("home", CancellationToken.None);
        await db.Received(1).ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(), Arg.Any<CommandFlags>());
    }
}
