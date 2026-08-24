using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Repositories.EF.Cache.Distributed;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public sealed class KyrolusEfDistributedCacheProviderTests
{
    private sealed record CachedUser(int Id, string Name);

    private static KyrolusEfDistributedCacheProvider CreateProvider()
    {
        var memoryCacheOptions = Options.Create(new MemoryDistributedCacheOptions());
        var distributedCache = new MemoryDistributedCache(memoryCacheOptions);
        return new KyrolusEfDistributedCacheProvider(distributedCache);
    }

    [Fact(DisplayName = "EfDistributedCacheProvider: SetAsync and GetAsync store and retrieve typed values")]
    public async Task SetAndGetAsync_StoresAndRetrieves()
    {
        var provider = CreateProvider();
        var user = new CachedUser(42, "Kyrolus");

        await provider.SetAsync("user:42", user, TimeSpan.FromMinutes(5));
        var retrieved = await provider.GetAsync<CachedUser>("user:42");

        retrieved.ShouldNotBeNull();
        retrieved.Id.ShouldBe(42);
        retrieved.Name.ShouldBe("Kyrolus");
    }

    [Fact(DisplayName = "EfDistributedCacheProvider: ExistsAsync and RemoveAsync work accurately")]
    public async Task ExistsAndRemoveAsync_Works()
    {
        var provider = CreateProvider();
        await provider.SetAsync("key:test", "value123", TimeSpan.FromMinutes(1));

        var exists = await provider.ExistsAsync("key:test");
        exists.ShouldBeTrue();

        await provider.RemoveAsync("key:test");
        var afterRemove = await provider.ExistsAsync("key:test");
        afterRemove.ShouldBeFalse();
    }

    [Fact(DisplayName = "EfDistributedCacheProvider: GetOrCreateAsync executes factory on cache miss and caches result")]
    public async Task GetOrCreateAsync_CachesFactoryResult()
    {
        var provider = CreateProvider();
        var factoryCallCount = 0;

        var result1 = await provider.GetOrCreateAsync("computed:key", _ =>
        {
            factoryCallCount++;
            return Task.FromResult(100);
        });

        result1.ShouldBe(100);
        factoryCallCount.ShouldBe(1);

        var result2 = await provider.GetOrCreateAsync("computed:key", _ =>
        {
            factoryCallCount++;
            return Task.FromResult(200);
        });

        result2.ShouldBe(100); // Cached
        factoryCallCount.ShouldBe(1); // Factory not called again
    }

    [Fact(DisplayName = "EfDistributedCacheProvider: DI registration AddKyrolusEfDistributedCacheProvider registers ICacheProvider")]
    public void ServiceCollectionExtensions_RegistersProvider()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddKyrolusEfDistributedCacheProvider();

        using var provider = services.BuildServiceProvider();
        var cacheProvider = provider.GetService<ICacheProvider>();

        cacheProvider.ShouldNotBeNull();
        cacheProvider.ShouldBeOfType<KyrolusEfDistributedCacheProvider>();
    }
}
