namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    [Fact(DisplayName = "GetByIdAsync caches results when cache is enabled and allowed")]
    public async Task GetByIdAsync_Caches_WhenEnabled()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        counter.Reset();
        var first = await repo.GetByIdAsync(Guid.Parse(productLaptopId));
        first.ShouldNotBeNull();
        cache.Count.ShouldBe(1);
        counter.Count.ShouldBeGreaterThan(0);

        counter.Reset();
        var second = await repo.GetByIdAsync(Guid.Parse(productLaptopId));
        second.ShouldNotBeNull();
        cache.Count.ShouldBe(1);
        counter.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetByIdAsync does not cache when includes are provided")]
    public async Task GetByIdAsync_DoesNotCache_WithIncludes()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        var item = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            includeProperties: ["Reviews"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        item.ShouldNotBeNull();
        cache.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetByIdAsync does not cache when includeGraph is provided")]
    public async Task GetByIdAsync_DoesNotCache_WithIncludeGraph()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        var item = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            includeProperties: null,
            includeGraph: new IncludeGraph<Product>(x => x.Reviews),
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        item.ShouldNotBeNull();
        cache.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetByIdAsync caches when includeExpressions are provided")]
    public async Task GetByIdAsync_Caches_WithIncludeExpressions()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        var item = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            asNoTracking: true,
            useSplitQuery: null,
            cancellationToken: default,
            e => e.Reviews,
            e => e.Store);

        item.ShouldNotBeNull();
        cache.Count.ShouldBe(1);
    }

    [Fact(DisplayName = "GetByIdAsync does not cache when read operations are not allowed")]
    public async Task GetByIdAsync_DoesNotCache_WhenReadOpsDisallow()
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

        cache.Clear();
        cache.Count.ShouldBe(0);

        var item = await repo.GetByIdAsync(Guid.Parse(productLaptopId));
        item.ShouldNotBeNull();
        cache.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetByIdAsync cache key varies by tenant scope")]
    public async Task GetByIdAsync_CacheKey_UsesTenantScope()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdAsync
        };

        var customFactory = WithPolicy(policy);
        using var client = customFactory.CreateClient();
        using var scope = customFactory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        static HttpRequestMessage BuildRequest(string id, string tenant)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/product/{id}");
            request.Headers.Add("X-Tenant-Id", tenant);
            return request;
        }

        var responseA = await client.SendAsync(BuildRequest(productLaptopId, "tenant-a"));
        responseA.EnsureSuccessStatusCode();
        cache.Count.ShouldBe(1);

        var responseB = await client.SendAsync(BuildRequest(productLaptopId, "tenant-b"));
        responseB.EnsureSuccessStatusCode();
        cache.Count.ShouldBe(2);
    }
}
