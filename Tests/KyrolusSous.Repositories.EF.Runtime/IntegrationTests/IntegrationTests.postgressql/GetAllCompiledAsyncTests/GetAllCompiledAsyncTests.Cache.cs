namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllCompiledAsyncTests;

public partial class GetAllCompiledAsyncTests
{
    [Fact(DisplayName = "GetAllCompiledAsync bypasses cache when operation is not allowed in read policy")]
    public async Task GetAllCompiledAsync_BypassesCache_WhenReadOperationIsNotAllowed()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.None
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        counter.Reset();
        (await repo.GetAllCompiledAsync(p => p.Price > 0m)).Count.ShouldBe(3);
        var firstCount = counter.Count;
        firstCount.ShouldBeGreaterThan(0);
        cache.Count.ShouldBe(0);

        counter.Reset();
        (await repo.GetAllCompiledAsync(p => p.Price > 0m)).Count.ShouldBe(3);
        var secondCount = counter.Count;
        secondCount.ShouldBeGreaterThan(0);
        cache.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllCompiledAsync caches results when cache is enabled and allowed")]
    public async Task GetAllCompiledAsync_Caches_WhenEnabled()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllCompiledAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        counter.Reset();
        var first = await repo.GetAllCompiledAsync(p => p.Price > 0m);
        first.Count.ShouldBe(3);
        cache.Count.ShouldBe(1);
        counter.Count.ShouldBeGreaterThan(0);

        counter.Reset();
        var second = await repo.GetAllCompiledAsync(p => p.Price > 0m);
        second.Count.ShouldBe(3);
        cache.Count.ShouldBe(1);
        counter.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllCompiledAsync cache key varies by filter and query options")]
    public async Task GetAllCompiledAsync_CacheKey_Varies_By_Filter_And_Options()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllCompiledAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        (await repo.GetAllCompiledAsync(p => p.Price > 0m, asNoTracking: true, useSplitQuery: false)).Count.ShouldBe(3);
        cache.Count.ShouldBe(1);

        (await repo.GetAllCompiledAsync(p => p.Price > 100m, asNoTracking: true, useSplitQuery: false)).Count.ShouldBe(2);
        cache.Count.ShouldBe(2);

        (await repo.GetAllCompiledAsync(p => p.Price > 0m, asNoTracking: false, useSplitQuery: false)).Count.ShouldBe(3);
        cache.Count.ShouldBe(3);

        (await repo.GetAllCompiledAsync(p => p.Price > 0m, asNoTracking: true, useSplitQuery: false)).Count.ShouldBe(3);
        cache.Count.ShouldBe(3);
    }

    [Fact(DisplayName = "GetAllCompiledAsync does not cache when global filter fallback is active")]
    public async Task GetAllCompiledAsync_DoesNotCache_WithGlobalFilter()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllCompiledAsync
        }.AddGlobalWhereFilter<Product>(p => p.Price > 100m);

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        counter.Reset();
        var first = await repo.GetAllCompiledAsync(p => p.Price > 0m);
        first.Count.ShouldBe(2);
        cache.Count.ShouldBe(0);
        counter.Count.ShouldBeGreaterThan(0);

        counter.Reset();
        var second = await repo.GetAllCompiledAsync(p => p.Price > 0m);
        second.Count.ShouldBe(2);
        cache.Count.ShouldBe(0);
        counter.Count.ShouldBeGreaterThan(0);
    }
}
