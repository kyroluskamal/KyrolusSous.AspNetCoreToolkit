using System.Net;

namespace KyrolusSous.Caching.UnitTests.Redis;

public sealed class RedisCacheProviderTests
{
    private sealed record ProductDto(int Id, string Name, decimal Price);

    [Fact(DisplayName = "KyrolusRedisCacheProvider: GetAsync returns deserialized object on cache hit")]
    public async Task GetAsync_Hit_ReturnsObject()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        muxer.GetDatabase().Returns(db);

        var serializer = new KyrolusJsonCacheSerializer();
        var product = new ProductDto(1, "Laptop", 1200m);
        var bytes = serializer.Serialize(product);

        db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult((RedisValue)bytes));

        var deps = new KyrolusRedisCacheDependencies(
            serializer,
            new KyrolusCacheKeyFactory("test"),
            new KyrolusRedisCacheOptions());

        var provider = new KyrolusRedisCacheProvider(muxer, deps);

        var result = await provider.GetAsync<ProductDto>("product:1");
        result.ShouldNotBeNull();
        result.Id.ShouldBe(1);
        result.Name.ShouldBe("Laptop");
        result.Price.ShouldBe(1200m);
    }

    [Fact(DisplayName = "KyrolusRedisCacheProvider: GetAsync returns null on cache miss")]
    public async Task GetAsync_Miss_ReturnsNull()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        muxer.GetDatabase().Returns(db);

        db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(RedisValue.Null));

        var deps = new KyrolusRedisCacheDependencies(
            new KyrolusJsonCacheSerializer(),
            new KyrolusCacheKeyFactory("test"),
            new KyrolusRedisCacheOptions());

        var provider = new KyrolusRedisCacheProvider(muxer, deps);

        var result = await provider.GetAsync<ProductDto>("nonexistent");
        result.ShouldBeNull();
    }

    [Fact(DisplayName = "KyrolusRedisCacheProvider: SetAsync saves serialized value to database")]
    public async Task SetAsync_SavesValue()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        muxer.GetDatabase().Returns(db);

        var deps = new KyrolusRedisCacheDependencies(
            new KyrolusJsonCacheSerializer(),
            new KyrolusCacheKeyFactory("test"),
            new KyrolusRedisCacheOptions());

        var provider = new KyrolusRedisCacheProvider(muxer, deps);
        var product = new ProductDto(2, "Phone", 800m);

        await provider.SetAsync("product:2", product, TimeSpan.FromMinutes(15));

        await db.Received().StringSetAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Any<Expiration>(),
            Arg.Any<ValueCondition>(),
            Arg.Any<CommandFlags>());
    }

    [Fact(DisplayName = "KyrolusRedisCacheProvider: SetAsync with KyrolusCacheEntryOptions saves sliding and tags")]
    public async Task SetAsync_WithOptions_SavesSlidingAndTags()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        muxer.GetDatabase().Returns(db);

        var deps = new KyrolusRedisCacheDependencies(
            new KyrolusJsonCacheSerializer(),
            new KyrolusCacheKeyFactory("test"),
            new KyrolusRedisCacheOptions());

        var provider = new KyrolusRedisCacheProvider(muxer, deps);

        var options = new KyrolusCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
            SlidingExpiration = TimeSpan.FromMinutes(10),
            Tags = ["electronics", "sales"],
            Jitter = TimeSpan.FromSeconds(5)
        };

        await provider.SetAsync("product:3", new ProductDto(3, "Monitor", 300m), options);

        await db.Received().StringSetAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Any<Expiration>(),
            Arg.Any<ValueCondition>(),
            Arg.Any<CommandFlags>());
    }

    [Fact(DisplayName = "KyrolusRedisCacheProvider: RemoveAsync and ExistsAsync execute database calls")]
    public async Task RemoveAndExists_Execute()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        muxer.GetDatabase().Returns(db);
        db.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(Task.FromResult(true));

        var deps = new KyrolusRedisCacheDependencies(
            new KyrolusJsonCacheSerializer(),
            new KyrolusCacheKeyFactory("test"),
            new KyrolusRedisCacheOptions());

        var provider = new KyrolusRedisCacheProvider(muxer, deps);

        var exists = await provider.ExistsAsync("product:1");
        exists.ShouldBeTrue();

        await provider.RemoveAsync("product:1");
        await db.Received().KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>());
    }

    [Fact(DisplayName = "KyrolusRedisCacheProvider: GetOrCreateAsync returns existing cached item on hit without invoking factory")]
    public async Task GetOrCreateAsync_Hit_DoesNotInvokeFactory()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        muxer.GetDatabase().Returns(db);

        var serializer = new KyrolusJsonCacheSerializer();
        var bytes = serializer.Serialize("CachedValue");
        db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(Task.FromResult((RedisValue)bytes));

        var deps = new KyrolusRedisCacheDependencies(
            serializer,
            new KyrolusCacheKeyFactory("test"),
            new KyrolusRedisCacheOptions());

        var provider = new KyrolusRedisCacheProvider(muxer, deps);

        var factoryCalled = false;
        var result = await provider.GetOrCreateAsync("key1", _ =>
        {
            factoryCalled = true;
            return Task.FromResult("FreshValue");
        });

        result.ShouldBe("CachedValue");
        factoryCalled.ShouldBeFalse();
    }

    [Fact(DisplayName = "KyrolusRedisCacheProvider: GetOrCreateAsync executes factory and caches result on miss")]
    public async Task GetOrCreateAsync_Miss_ExecutesFactoryAndCaches()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        muxer.GetDatabase().Returns(db);

        db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(Task.FromResult(RedisValue.Null));

        var deps = new KyrolusRedisCacheDependencies(
            new KyrolusJsonCacheSerializer(),
            new KyrolusCacheKeyFactory("test"),
            new KyrolusRedisCacheOptions { LockStrategy = KyrolusRedisLockStrategy.Disabled });

        var provider = new KyrolusRedisCacheProvider(muxer, deps);

        var factoryCalled = false;
        var result = await provider.GetOrCreateAsync("key:fresh", _ =>
        {
            factoryCalled = true;
            return Task.FromResult("FreshData");
        });

        result.ShouldBe("FreshData");
        factoryCalled.ShouldBeTrue();
    }

    [Fact(DisplayName = "KyrolusRedisCacheProvider: GetOrCreateAsync with Lua lock acquires lock, executes factory, and releases")]
    public async Task GetOrCreateAsync_WithLock_AcquiresAndReleases()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        muxer.GetDatabase().Returns(db);

        db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(Task.FromResult(RedisValue.Null));
        db.ScriptEvaluateAsync(Arg.Any<string>(), Arg.Any<RedisKey[]>(), Arg.Any<RedisValue[]>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(RedisResult.Create((RedisValue)1)));

        var deps = new KyrolusRedisCacheDependencies(
            new KyrolusJsonCacheSerializer(),
            new KyrolusCacheKeyFactory("test"),
            new KyrolusRedisCacheOptions { LockStrategy = KyrolusRedisLockStrategy.Lua });

        var provider = new KyrolusRedisCacheProvider(muxer, deps);

        var result = await provider.GetOrCreateAsync("key:locked", _ => Task.FromResult("LockedData"));
        result.ShouldBe("LockedData");
    }

    [Fact(DisplayName = "KyrolusRedisCacheProvider: IncrementAsync and DecrementAsync invoke database StringIncrement and StringDecrement")]
    public async Task IncrementAndDecrement_InvokeDatabase()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        muxer.GetDatabase().Returns(db);

        db.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(Task.FromResult(10L));
        db.StringDecrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(Task.FromResult(9L));

        var deps = new KyrolusRedisCacheDependencies(
            new KyrolusJsonCacheSerializer(),
            new KyrolusCacheKeyFactory("test"),
            new KyrolusRedisCacheOptions());

        var provider = new KyrolusRedisCacheProvider(muxer, deps);

        var inc = await provider.IncrementAsync("hits", 5);
        inc.ShouldBe(10L);

        var dec = await provider.DecrementAsync("hits", 1);
        dec.ShouldBe(9L);
    }

    [Fact(DisplayName = "KyrolusRedisCacheProvider: Batch operations (GetManyAsync, SetManyAsync, RemoveManyAsync) work")]
    public async Task BatchOperations_Work()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        muxer.GetDatabase().Returns(db);

        var serializer = new KyrolusJsonCacheSerializer();
        var item1 = new ProductDto(1, "Item1", 10m);
        var item2 = new ProductDto(2, "Item2", 20m);

        db.StringGetAsync(Arg.Any<RedisKey[]>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(new RedisValue[] { serializer.Serialize(item1), serializer.Serialize(item2) }));

        var deps = new KyrolusRedisCacheDependencies(
            serializer,
            new KyrolusCacheKeyFactory("test"),
            new KyrolusRedisCacheOptions());

        var provider = new KyrolusRedisCacheProvider(muxer, deps);

        // GetMany
        var items = await provider.GetManyAsync<ProductDto>(["item:1", "item:2"]);
        items.Count.ShouldBe(2);
        items["item:1"]!.Name.ShouldBe("Item1");
        items["item:2"]!.Name.ShouldBe("Item2");

        var kvps = items.Select(kv => new KeyValuePair<string, ProductDto>(kv.Key, kv.Value!)).ToArray();

        // SetMany
        await provider.SetManyAsync<ProductDto>(kvps, TimeSpan.FromMinutes(10));
        await provider.SetManyAsync<ProductDto>(kvps, new KyrolusCacheEntryOptions());

        // RemoveMany
        await provider.RemoveManyAsync(["item:1", "item:2"]);
    }

    [Fact(DisplayName = "KyrolusRedisCacheProvider: Hashes (HashSetAsync, HashGetAsync, HashGetAllAsync, HashDeleteAsync) work")]
    public async Task HashOperations_Work()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        muxer.GetDatabase().Returns(db);

        var serializer = new KyrolusJsonCacheSerializer();
        var bytes = serializer.Serialize("FieldValue");

        db.HashSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<RedisValue>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));
        db.HashGetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult((RedisValue)bytes));
        db.HashGetAllAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(new HashEntry[] { new HashEntry("field1", bytes) }));
        db.HashDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));

        var deps = new KyrolusRedisCacheDependencies(
            serializer,
            new KyrolusCacheKeyFactory("test"),
            new KyrolusRedisCacheOptions());

        var provider = new KyrolusRedisCacheProvider(muxer, deps);

        var set = await provider.HashSetAsync("h:user:1", "name", "FieldValue");
        set.ShouldBeTrue();

        var get = await provider.HashGetAsync<string>("h:user:1", "name");
        get.ShouldBe("FieldValue");

        var all = await provider.HashGetAllAsync<string>("h:user:1");
        all.Count.ShouldBe(1);
        all["field1"].ShouldBe("FieldValue");

        var del = await provider.HashDeleteAsync("h:user:1", "name");
        del.ShouldBeTrue();
    }

    [Fact(DisplayName = "KyrolusRedisCacheProvider: RemoveByTagAsync and RemoveKeysByPatternAsync work")]
    public async Task TagAndPatternRemovals_Work()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        muxer.GetDatabase().Returns(db);

        db.SetMembersAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(new RedisValue[] { "test:item:1" }));

        var deps = new KyrolusRedisCacheDependencies(
            new KyrolusJsonCacheSerializer(),
            new KyrolusCacheKeyFactory("test"),
            new KyrolusRedisCacheOptions { PatternRemovalStrategy = KyrolusRedisPatternRemovalStrategy.KeyIndex });

        var provider = new KyrolusRedisCacheProvider(muxer, deps);

        await provider.RemoveByTagAsync("catalog");
        await provider.RemoveKeysByPatternAsync("item:*");
    }

    [Fact(DisplayName = "KyrolusRedisCacheProvider: RemoveKeysByPatternAsync with ServerScan iterates server keys")]
    public async Task RemoveKeysByPatternAsync_ServerScan_Works()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        var server = Substitute.For<IServer>();
        var endPoint = new IPEndPoint(IPAddress.Loopback, 6379);

        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        muxer.GetDatabase().Returns(db);
        muxer.GetEndPoints(Arg.Any<bool>()).Returns([endPoint]);
        muxer.GetServer(endPoint, Arg.Any<object?>()).Returns(server);
        server.IsConnected.Returns(true);
        server.IsReplica.Returns(false);
        server.Features.Returns(new RedisFeatures(new Version(6, 0)));
        server.Keys(Arg.Any<int>(), Arg.Any<RedisValue>(), Arg.Any<int>(), Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CommandFlags>())
            .Returns(new RedisKey[] { "test:pattern:1" });

        var deps = new KyrolusRedisCacheDependencies(
            new KyrolusJsonCacheSerializer(),
            new KyrolusCacheKeyFactory("test"),
            new KyrolusRedisCacheOptions { PatternRemovalStrategy = KyrolusRedisPatternRemovalStrategy.ServerScan });

        var provider = new KyrolusRedisCacheProvider(muxer, deps);

        await provider.RemoveKeysByPatternAsync("pattern:*");
    }

    [Fact(DisplayName = "KyrolusRedisCacheProvider: Graceful fallback handles Redis exceptions and returns default")]
    public async Task GracefulFallback_HandlesException_ReturnsDefault()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.IsConnected.Returns(true);
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        muxer.GetDatabase().Returns(db);

        db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns<Task<RedisValue>>(_ => throw new RedisConnectionException(ConnectionFailureType.SocketFailure, "Redis connection dropped"));

        var deps = new KyrolusRedisCacheDependencies(
            new KyrolusJsonCacheSerializer(),
            new KyrolusCacheKeyFactory("test"),
            new KyrolusRedisCacheOptions { EnableGracefulFallback = true });

        var provider = new KyrolusRedisCacheProvider(muxer, deps);

        var result = await provider.GetAsync<ProductDto>("product:1");
        result.ShouldBeNull();
    }
}
