using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public enum KeyType { Single, Composite }

public partial class GetAllIncludingDeletedAsyncTests
{
    readonly KyrolusRepositoryPolicy Policy = new()
    {
        DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
        DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllIncludingDeletedAsync
    };
    [Theory(DisplayName = "GetAllIncludingDeletedAsync caches results when enabled and allowed")]
    [InlineData(KeyType.Single)]
    [InlineData(KeyType.Composite)]
    public async Task GetAllIncludingDeletedAsync_Caches_WhenEnabled(KeyType keyTYpe)
    {
        if (keyTYpe == KeyType.Single)
        {
            await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, repo, sp) =>
            {
                var cache = sp?.GetRequiredService<InMemoryCacheProvider>();
                var counter = sp?.GetRequiredService<CommandCounterInterceptor>();
                await AssertCachingWorks<Product>(cache!, counter!, async ()
                =>
                {
                    return await repo.GetAllIncludingDeletedAsync();
                });
            }, null, Policy);
            await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, repo, sp) =>
            {
                var cache = sp?.GetRequiredService<InMemoryCacheProvider>();
                var counter = sp?.GetRequiredService<CommandCounterInterceptor>();
                await AssertCachingWorks<Product>(cache!, counter!, async ()
                =>
                {
                    return await repo.GetAllIncludingDeletedAsync(null, null, null, null, default, null);
                });
            }, null, Policy);
        }
        else
        {
            await WithSoftDeletedAsync_CompositeKey<Review>(DataSeeder.ReviewLapTopKey, async (_, reviews, _, repo, sp) =>
            {
                var cache = sp?.GetRequiredService<InMemoryCacheProvider>();
                var counter = sp?.GetRequiredService<CommandCounterInterceptor>();
                await AssertCachingWorks(cache!, counter!, async () =>
                {
                    return await repo.GetAllIncludingDeletedAsync(null, null, null, null, default, null);
                });
            }, null, Policy);
        }
    }
    [Theory(DisplayName = "GetAllIncludingDeletedAsync does not cache when filter is provided")]
    [InlineData(KeyType.Single)]
    [InlineData(KeyType.Composite)]
    public async Task GetAllIncludingDeletedAsync_DoesNotCache_WithFilter(KeyType keyType)
    {
        await TestNoCacheSingleKey(keyType, repo => repo.GetAllIncludingDeletedAsync(p => p.Price > 0m));
        await TestNoCacheSingleKey(keyType, repo => repo.GetAllIncludingDeletedAsync(p => p.Price > 0m, null, null, null, default, null));
        await TestNoCacheCompositeKey(keyType, repo => repo.GetAllIncludingDeletedAsync(p => p.Rating > 0));
        await TestNoCacheCompositeKey(keyType, repo => repo.GetAllIncludingDeletedAsync(p => p.Rating > 0, null, null, null, default, null));
    }
    [Theory(DisplayName = "GetAllIncludingDeletedAsync does not cache when includes are provided")]
    [InlineData(KeyType.Single)]
    [InlineData(KeyType.Composite)]
    public async Task GetAllIncludingDeletedAsync_DoesNotCache_WithIncludes(KeyType keyType)
    {
        await TestNoCacheSingleKey(keyType, repo => repo.GetAllIncludingDeletedAsync(includeProperties: [nameof(Product.Reviews)]));
        await TestNoCacheCompositeKey(keyType, repo => repo.GetAllIncludingDeletedAsync(includeProperties: [nameof(Review.Product)]));
    }
    [Theory(DisplayName = "GetAllIncludingDeletedAsync does not cache when orderBy is provided")]
    [InlineData(KeyType.Single)]
    [InlineData(KeyType.Composite)]
    public async Task GetAllIncludingDeletedAsync_DoesNotCache_WithOrderBy(KeyType keyType)
    {
        await TestNoCacheSingleKey(keyType, repo => repo.GetAllIncludingDeletedAsync(orderBy: q => q.OrderBy(p => p.Price)));
        await TestNoCacheSingleKey(keyType, repo => repo.GetAllIncludingDeletedAsync(null, orderBy: q => q.OrderBy(p => p.Price), null, null, default, null));
        await TestNoCacheCompositeKey(keyType, repo => repo.GetAllIncludingDeletedAsync(orderBy: q => q.OrderBy(p => p.Rating)));
        await TestNoCacheCompositeKey(keyType, repo => repo.GetAllIncludingDeletedAsync(null, orderBy: q => q.OrderBy(p => p.Rating), null, null, default, null));
    }
    [Theory(DisplayName = "GetAllIncludingDeletedAsync does not cache when includeGraph is provided")]
    [InlineData(KeyType.Single)]
    [InlineData(KeyType.Composite)]
    public async Task GetAllIncludingDeletedAsync_DoesNotCache_WithIncludeGraph(KeyType keyType)
    {
        await TestNoCacheSingleKey(keyType, repo => repo.GetAllIncludingDeletedAsync(includeGraph: new IncludeGraph<Product>(x => x.Reviews)));
        await TestNoCacheCompositeKey(keyType, repo => repo.GetAllIncludingDeletedAsync(includeGraph: new IncludeGraph<Review>(x => x.Product)));
    }
    [Theory(DisplayName = "GetAllIncludingDeletedAsync does not cache when includeExpressions are provided")]
    [InlineData(KeyType.Single)]
    [InlineData(KeyType.Composite)]
    public async Task GetAllIncludingDeletedAsync_DoesNotCache_WithIncludeExpressions(KeyType keyType)
    {
        await TestNoCacheSingleKey(keyType, repo => repo.GetAllIncludingDeletedAsync(includeExpressions: p => p.Reviews));
        await TestNoCacheCompositeKey(keyType, repo => repo.GetAllIncludingDeletedAsync(includeExpressions: p => p.Product));
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync does not cache when read operations are not allowed")]
    [InlineData(KeyType.Single)]
    [InlineData(KeyType.Composite)]
    public async Task GetAllIncludingDeletedAsync_DoesNotCache_WhenReadOpsDisallow(KeyType keyType)
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.None
        };
        if (keyType == KeyType.Single)
            await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, repo, sp) =>
            {
                var cache = sp?.GetRequiredService<InMemoryCacheProvider>();
                await AssertCachingNotWorks(cache!, async () => await repo.GetAllIncludingDeletedAsync());
            }, null, policy);
        else
            await WithSoftDeletedAsync_CompositeKey<Review>(DataSeeder.ReviewLapTopKey, async (_, reviews, _, repo, sp) =>
            {
                var cache = sp?.GetRequiredService<InMemoryCacheProvider>();
                await AssertCachingNotWorks(cache!, async () => await repo.GetAllIncludingDeletedAsync());
            }, null, policy);
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync cache key varies by tenant scope")]
    public async Task GetAllIncludingDeletedAsync_CacheKey_UsesTenantScope()
    {
        var customFactory = WithPolicy(Policy).WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICacheKeyContext>();
                services.AddScoped<ICacheKeyContext>(_ => new TestCacheKeyContext());
            });
        });

        var cache = customFactory.Services.GetRequiredService<InMemoryCacheProvider>();
        cache.Clear();
        cache.Count.ShouldBe(0);

        TenantScope.Value = "tenant-a";
        using (var scopeA = customFactory.Services.CreateScope())
        {
            var repoA = scopeA.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var itemsA = await repoA.GetAllIncludingDeletedAsync();
            itemsA.Count.ShouldBe(3);
            cache.Count.ShouldBe(1);
        }

        TenantScope.Value = "tenant-b";
        using var scopeB = customFactory.Services.CreateScope();
        var repoB = scopeB.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var itemsB = await repoB.GetAllIncludingDeletedAsync();
        itemsB.Count.ShouldBe(3);
        cache.Count.ShouldBe(2);
    }
    private static async Task AssertCachingWorks<TEntity>(
    InMemoryCacheProvider cache,
    CommandCounterInterceptor counter,
    Func<Task<IReadOnlyList<TEntity>?>> act,
    int expectedCount = 3)
    {
        counter.Reset();
        var first = await act();
        first.ShouldNotBeNull();
        first.Count.ShouldBe(expectedCount);
        cache.Count.ShouldBe(1);
        counter.Count.ShouldBeGreaterThan(0);

        counter.Reset();
        var second = await act();
        second.ShouldNotBeNull();
        second.Count.ShouldBe(expectedCount);
        cache.Count.ShouldBe(1);
        counter.Count.ShouldBe(0);
    }
    private static async Task AssertCachingNotWorks<TEntity>(
    InMemoryCacheProvider cache,
    Func<Task<IReadOnlyList<TEntity>?>> act,
    int expectedCount = 3)
    {
        cache.Clear();
        cache.Count.ShouldBe(0);
        var items = await act();
        items.ShouldNotBeNull();
        items.Count.ShouldBe(expectedCount);
        cache.Count.ShouldBe(0);
    }

    private async Task TestNoCacheSingleKey(KeyType keyType, Func<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>, Task<IReadOnlyList<Product>>> act)
    {
        if (keyType == KeyType.Single)
            await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, repo, sp) =>
            {
                var cache = sp?.GetRequiredService<InMemoryCacheProvider>();
                await AssertCachingNotWorks(cache!, async () => { return await act(repo); });

            }, null, Policy);
    }
    private async Task TestNoCacheCompositeKey(KeyType keyType, Func<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>, Task<IReadOnlyList<Review>>> act)
    {
        if (keyType == KeyType.Composite)
            await WithSoftDeletedAsync_CompositeKey<Review>(DataSeeder.ReviewLapTopKey, async (_, reviews, _, repo, sp) =>
            {
                var cache = sp?.GetRequiredService<InMemoryCacheProvider>();
                await AssertCachingNotWorks(cache!, async () =>
                await act(repo));
            }, null, Policy);
    }
}
