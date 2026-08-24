using System.Security.Cryptography;

namespace KyrolusSous.Caching.UnitTests.Redis;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact(DisplayName = "ServiceCollectionExtensions: AddKyrolusRedisCacheProvider registers core services in DI")]
    public void AddKyrolusRedisCacheProvider_RegistersServices()
    {
        var services = new ServiceCollection();
        var muxer = Substitute.For<IConnectionMultiplexer>();
        services.AddSingleton(muxer);

        services.AddKyrolusRedisCacheProvider(options =>
        {
            options.WithKeyPrefix("test");
        });

        var sp = services.BuildServiceProvider();

        sp.GetService<ICacheProvider>().ShouldNotBeNull();
        sp.GetService<IDistributedLockProvider>().ShouldNotBeNull();
        sp.GetService<IKyrolusRedisPubSub>().ShouldNotBeNull();
        sp.GetService<IKyrolusCacheSerializer>().ShouldNotBeNull();
        sp.GetService<IKyrolusCacheKeyFactory>().ShouldNotBeNull();
    }

    [Fact(DisplayName = "ServiceCollectionExtensions: AddKyrolusRedisCache binds from IConfiguration")]
    public void AddKyrolusRedisCache_WithConfiguration_BindsOptions()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:KeyPrefix"] = "cfg-prefix",
                ["Redis:DefaultTtl"] = "00:15:00",
                ["ConnectionStrings:Redis"] = "localhost:6379"
            })
            .Build();

        var services = new ServiceCollection();
        var muxer = Substitute.For<IConnectionMultiplexer>();
        services.AddSingleton(muxer);

        services.AddKyrolusRedisCache(config);

        var sp = services.BuildServiceProvider();
        var options = sp.GetService<KyrolusRedisCacheOptions>();
        options.ShouldNotBeNull();
        options.KeyPrefix.ShouldBe("cfg-prefix");
        options.DefaultTtl.ShouldBe(TimeSpan.FromMinutes(15));
        options.ConnectionString.ShouldBe("localhost:6379");
    }

    [Fact(DisplayName = "ServiceCollectionExtensions: AddKyrolusRedisDistributedCache registers IDistributedCache adapter")]
    public void AddKyrolusRedisDistributedCache_RegistersAdapter()
    {
        var services = new ServiceCollection();
        var muxer = Substitute.For<IConnectionMultiplexer>();
        services.AddSingleton(muxer);
        services.AddKyrolusRedisDistributedCache();

        var sp = services.BuildServiceProvider();
        sp.GetService<IDistributedCache>().ShouldNotBeNull();
        sp.GetService<IDistributedCache>().ShouldBeOfType<KyrolusRedisDistributedCacheAdapter>();
    }

    [Fact(DisplayName = "ServiceCollectionExtensions: AddKyrolusRedisOutputCache registers IOutputCacheStore")]
    public void AddKyrolusRedisOutputCache_RegistersOutputStore()
    {
        var services = new ServiceCollection();
        var muxer = Substitute.For<IConnectionMultiplexer>();
        services.AddSingleton(muxer);
        services.AddKyrolusRedisOutputCache();

        var sp = services.BuildServiceProvider();
        sp.GetService<IOutputCacheStore>().ShouldNotBeNull();
        sp.GetService<IOutputCacheStore>().ShouldBeOfType<KyrolusRedisOutputCacheStore>();
    }

    [Fact(DisplayName = "ServiceCollectionExtensions: AddKyrolusCacheLoggingObserver registers observer")]
    public void AddKyrolusCacheLoggingObserver_RegistersObserver()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusCacheLoggingObserver();

        var sp = services.BuildServiceProvider();
        sp.GetService<IKyrolusCacheObserver>().ShouldNotBeNull();
        sp.GetService<IKyrolusCacheObserver>().ShouldBeOfType<KyrolusCacheLoggingObserver>();
    }

    [Fact(DisplayName = "ServiceCollectionExtensions: AddKyrolusRedisNearCache registers NearCacheProvider in DI")]
    public void AddKyrolusRedisNearCache_RegistersNearCache()
    {
        var services = new ServiceCollection();
        var muxer = Substitute.For<IConnectionMultiplexer>();
        services.AddSingleton(muxer);

        services.AddKyrolusRedisNearCache(
            opts => opts.WithKeyPrefix("near"),
            nearOpts =>
            {
                nearOpts.WithInvalidationChannel("test-chan").WithDefaultL1Ttl(TimeSpan.FromMinutes(2));
                nearOpts.SubscribeInvalidations = false;
            });

        var sp = services.BuildServiceProvider();

        sp.GetService<KyrolusRedisNearCacheProvider>().ShouldNotBeNull();
        sp.GetService<ICacheProvider>().ShouldNotBeNull();
        sp.GetService<ICacheProvider>().ShouldBeOfType<KyrolusRedisNearCacheProvider>();
    }

    [Fact(DisplayName = "ServiceCollectionExtensions: AddKyrolusRedisInvalidationBus registers invalidation bus")]
    public void AddKyrolusRedisInvalidationBus_RegistersBus()
    {
        var services = new ServiceCollection();
        var muxer = Substitute.For<IConnectionMultiplexer>();
        services.AddSingleton(muxer);

        services.AddKyrolusRedisInvalidationBus();

        var sp = services.BuildServiceProvider();
        sp.GetService<IKyrolusCacheInvalidationBus>().ShouldNotBeNull();
        sp.GetService<IKyrolusCacheInvalidationBus>().ShouldBeOfType<KyrolusRedisInvalidationBus>();
    }

    [Fact(DisplayName = "ServiceCollectionExtensions: AddKyrolusRedisCacheHealthChecks registers health check")]
    public void AddKyrolusRedisCacheHealthChecks_RegistersCheck()
    {
        var services = new ServiceCollection();
        var muxer = Substitute.For<IConnectionMultiplexer>();
        services.AddSingleton(muxer);

        services.AddHealthChecks().AddKyrolusRedisCacheHealthChecks();

        var sp = services.BuildServiceProvider();
        var options = sp.GetService<KyrolusRedisCacheHealthCheckOptions>();
        options.ShouldNotBeNull();
    }

    [Fact(DisplayName = "ServiceCollectionExtensions: Full pipeline (Compression + Encryption) configures IKyrolusCacheSerializer")]
    public void FullPipeline_ConfiguresTransformingSerializer()
    {
        var services = new ServiceCollection();
        var muxer = Substitute.For<IConnectionMultiplexer>();
        services.AddSingleton(muxer);

        var key = RandomNumberGenerator.GetBytes(32);
        var iv = RandomNumberGenerator.GetBytes(16);

        services.AddKyrolusRedisCacheProvider(options =>
        {
            options.WithBrotliCompression();
            options.WithEncryption(key, iv);
        });

        var sp = services.BuildServiceProvider();
        var serializer = sp.GetService<IKyrolusCacheSerializer>();

        serializer.ShouldNotBeNull();
        serializer.ShouldBeOfType<KyrolusTransformingCacheSerializer>();
    }
}
