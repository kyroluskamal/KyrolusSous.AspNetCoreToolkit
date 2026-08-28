using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    private readonly KyrolusRepositoryPolicy Policy = new()
    {
        DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
        DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllIncludingDeletedAsync
    };

    public static TheoryData<string> EntityCases => new()
    {
        "product",
        "review"
    };

    [Theory(DisplayName = "GetAllIncludingDeletedAsync caches results when enabled and allowed")]
    [MemberData(nameof(EntityCases))]
    public Task GetAllIncludingDeletedAsync_Caches_WhenEnabled(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return caseId == "product"
            ? AssertCacheEnabledForProduct()
            : AssertCacheEnabledForReview();
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync does not cache when filter is provided")]
    [MemberData(nameof(EntityCases))]
    public Task GetAllIncludingDeletedAsync_DoesNotCache_WithFilter(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return caseId == "product"
            ? AssertNoCacheWithFilterProduct()
            : AssertNoCacheWithFilterReview();
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync does not cache when includes are provided")]
    [MemberData(nameof(EntityCases))]
    public Task GetAllIncludingDeletedAsync_DoesNotCache_WithIncludes(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return caseId == "product"
            ? AssertNoCacheWithIncludesProduct()
            : AssertNoCacheWithIncludesReview();
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync does not cache when orderBy is provided")]
    [MemberData(nameof(EntityCases))]
    public Task GetAllIncludingDeletedAsync_DoesNotCache_WithOrderBy(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return caseId == "product"
            ? AssertNoCacheWithOrderByProduct()
            : AssertNoCacheWithOrderByReview();
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync does not cache when includeGraph is provided")]
    [MemberData(nameof(EntityCases))]
    public Task GetAllIncludingDeletedAsync_DoesNotCache_WithIncludeGraph(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return caseId == "product"
            ? AssertNoCacheWithIncludeGraphProduct()
            : AssertNoCacheWithIncludeGraphReview();
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync does not cache when includeExpressions are provided")]
    [MemberData(nameof(EntityCases))]
    public Task GetAllIncludingDeletedAsync_DoesNotCache_WithIncludeExpressions(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return caseId == "product"
            ? AssertNoCacheWithIncludeExpressionsProduct()
            : AssertNoCacheWithIncludeExpressionsReview();
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync does not cache when read operations are not allowed")]
    [MemberData(nameof(EntityCases))]
    public Task GetAllIncludingDeletedAsync_DoesNotCache_WhenReadOpsDisallow(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return caseId == "product"
            ? AssertNoCacheWhenReadOpsDisallowProduct()
            : AssertNoCacheWhenReadOpsDisallowReview();
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync cache key varies by tenant scope")]
    public async Task GetAllIncludingDeletedAsync_CacheKey_UsesTenantScope()
    {
        var customFactory = WithPolicy(Policy).WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IKyrolusCacheKeyContext>();
                services.AddScoped<IKyrolusCacheKeyContext>(_ => new TestCacheKeyContext());
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

    private async Task AssertCacheEnabledForProduct()
    {
        await WithProductSoftDeleted(async (repo, sp) =>
        {
            var cache = sp.GetRequiredService<InMemoryCacheProvider>();
            var counter = sp.GetRequiredService<CommandCounterInterceptor>();
            await AssertCachingWorks(cache, counter, async () => await repo.GetAllIncludingDeletedAsync());
        }, policy: Policy);

        await WithProductSoftDeleted(async (repo, sp) =>
        {
            var cache = sp.GetRequiredService<InMemoryCacheProvider>();
            var counter = sp.GetRequiredService<CommandCounterInterceptor>();
            await AssertCachingWorks(cache, counter, async () =>
                await repo.GetAllIncludingDeletedAsync(null, null, null, null, default, null));
        }, policy: Policy);
    }

    private Task AssertCacheEnabledForReview()
        => WithReviewSoftDeleted(async (repo, sp) =>
        {
            var cache = sp.GetRequiredService<InMemoryCacheProvider>();
            var counter = sp.GetRequiredService<CommandCounterInterceptor>();
            await AssertCachingWorks(cache, counter, async () =>
                await repo.GetAllIncludingDeletedAsync(null, null, null, null, default, null));
        }, policy: Policy);

    private async Task AssertNoCacheWithFilterProduct()
    {
        await AssertNoCacheProduct(repo => repo.GetAllIncludingDeletedAsync(p => p.Price > 0m));
        await AssertNoCacheProduct(repo => repo.GetAllIncludingDeletedAsync(p => p.Price > 0m, null, null, null, default, null));
    }

    private async Task AssertNoCacheWithFilterReview()
    {
        await AssertNoCacheReview(repo => repo.GetAllIncludingDeletedAsync(p => p.Rating > 0));
        await AssertNoCacheReview(repo => repo.GetAllIncludingDeletedAsync(p => p.Rating > 0, null, null, null, default, null));
    }

    private Task AssertNoCacheWithIncludesProduct()
        => AssertNoCacheProduct(repo => repo.GetAllIncludingDeletedAsync(includeProperties: [nameof(Product.Reviews)]));

    private Task AssertNoCacheWithIncludesReview()
        => AssertNoCacheReview(repo => repo.GetAllIncludingDeletedAsync(includeProperties: [nameof(Review.Product)]));

    private async Task AssertNoCacheWithOrderByProduct()
    {
        await AssertNoCacheProduct(repo => repo.GetAllIncludingDeletedAsync(orderBy: q => q.OrderBy(p => p.Price)));
        await AssertNoCacheProduct(repo => repo.GetAllIncludingDeletedAsync(null, orderBy: q => q.OrderBy(p => p.Price), null, null, default, null));
    }

    private async Task AssertNoCacheWithOrderByReview()
    {
        await AssertNoCacheReview(repo => repo.GetAllIncludingDeletedAsync(orderBy: q => q.OrderBy(p => p.Rating)));
        await AssertNoCacheReview(repo => repo.GetAllIncludingDeletedAsync(null, orderBy: q => q.OrderBy(p => p.Rating), null, null, default, null));
    }

    private Task AssertNoCacheWithIncludeGraphProduct()
        => AssertNoCacheProduct(repo => repo.GetAllIncludingDeletedAsync(includeGraph: new IncludeGraph<Product>(x => x.Reviews)));

    private Task AssertNoCacheWithIncludeGraphReview()
        => AssertNoCacheReview(repo => repo.GetAllIncludingDeletedAsync(includeGraph: new IncludeGraph<Review>(x => x.Product)));

    private Task AssertNoCacheWithIncludeExpressionsProduct()
        => AssertNoCacheProduct(repo => repo.GetAllIncludingDeletedAsync(includeExpressions: p => p.Reviews));

    private Task AssertNoCacheWithIncludeExpressionsReview()
        => AssertNoCacheReview(repo => repo.GetAllIncludingDeletedAsync(includeExpressions: p => p.Product));

    private Task AssertNoCacheWhenReadOpsDisallowProduct()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.None
        };

        return WithProductSoftDeleted(async (repo, sp) =>
        {
            var cache = sp.GetRequiredService<InMemoryCacheProvider>();
            await AssertCachingNotWorks(cache, async () => await repo.GetAllIncludingDeletedAsync());
        }, policy: policy);
    }

    private Task AssertNoCacheWhenReadOpsDisallowReview()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.None
        };

        return WithReviewSoftDeleted(async (repo, sp) =>
        {
            var cache = sp.GetRequiredService<InMemoryCacheProvider>();
            await AssertCachingNotWorks(cache, async () => await repo.GetAllIncludingDeletedAsync());
        }, policy: policy);
    }

    private async Task AssertNoCacheProduct(
        Func<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>, Task<IReadOnlyList<Product>>> act)
        => await WithProductSoftDeleted(async (repo, sp) =>
        {
            var cache = sp.GetRequiredService<InMemoryCacheProvider>();
            await AssertCachingNotWorks(cache, async () => await act(repo));
        }, policy: Policy);

    private async Task AssertNoCacheReview(
        Func<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>, Task<IReadOnlyList<Review>>> act)
        => await WithReviewSoftDeleted(async (repo, sp) =>
        {
            var cache = sp.GetRequiredService<InMemoryCacheProvider>();
            await AssertCachingNotWorks(cache, async () => await act(repo));
        }, policy: Policy);

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
}
