namespace KyrolusSous.Caching.UnitTests.Redis;

public sealed class KyrolusRedisNearCacheProviderTests
{
    private sealed record CacheItem(int Id, string Value);

    [Fact(DisplayName = "KyrolusRedisNearCacheProvider: L1 Memory cache hit serves data instantly without querying L2 Redis")]
    public async Task NearCache_L1Hit_ServesFromMemory()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);

        var subscriber = Substitute.For<ISubscriber>();
        muxer.GetSubscriber(Arg.Any<object>()).Returns(subscriber);

        var serializer = new KyrolusJsonCacheSerializer();
        var deps = new KyrolusRedisCacheDependencies(
            serializer,
            new KyrolusCacheKeyFactory("test"),
            new KyrolusRedisCacheOptions());

        var nearCache = new KyrolusRedisNearCacheProvider(
            memoryCache,
            muxer,
            deps,
            new KyrolusRedisNearCacheOptions { SubscribeInvalidations = false, PublishInvalidations = false });

        // Pre-populate L1 memory cache
        var item = new CacheItem(1, "Prepopulated");
        await nearCache.SetAsync("item:1", item, TimeSpan.FromMinutes(5));

        // GetAsync should be an L1 hit
        var result = await nearCache.GetAsync<CacheItem>("item:1");
        result.ShouldNotBeNull();
        result.Id.ShouldBe(1);
        result.Value.ShouldBe("Prepopulated");

        // StringGetAsync on L2 database should NOT have been called for GetAsync because L1 was hit
        await db.DidNotReceive().StringGetAsync((RedisKey)"test:item:1", Arg.Any<CommandFlags>());
    }

    [Fact(DisplayName = "KyrolusRedisNearCacheProvider: L1 Miss falls back to L2 Redis and populates L1")]
    public async Task NearCache_L1Miss_ReadsL2AndPopulatesL1()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);

        var serializer = new KyrolusJsonCacheSerializer();
        var item = new CacheItem(2, "FromRedis");
        var bytes = serializer.Serialize(item);
        db.StringGetAsync((RedisKey)"test:item:2", Arg.Any<CommandFlags>()).Returns(Task.FromResult((RedisValue)bytes));

        var deps = new KyrolusRedisCacheDependencies(
            serializer,
            new KyrolusCacheKeyFactory("test"),
            new KyrolusRedisCacheOptions());

        var nearCache = new KyrolusRedisNearCacheProvider(
            memoryCache,
            muxer,
            deps,
            new KyrolusRedisNearCacheOptions { SubscribeInvalidations = false, PublishInvalidations = false });

        // 1st call -> L1 miss, L2 hit -> Populates L1
        var firstResult = await nearCache.GetAsync<CacheItem>("item:2");
        firstResult.ShouldNotBeNull();
        firstResult.Id.ShouldBe(2);

        // 2nd call -> L1 hit directly from memory!
        var secondResult = await nearCache.GetAsync<CacheItem>("item:2");
        secondResult.ShouldNotBeNull();
        secondResult.Id.ShouldBe(2);

        // L2 database should have been queried exactly once for this key
        await db.Received(1).StringGetAsync((RedisKey)"test:item:2", Arg.Any<CommandFlags>());
    }
}
