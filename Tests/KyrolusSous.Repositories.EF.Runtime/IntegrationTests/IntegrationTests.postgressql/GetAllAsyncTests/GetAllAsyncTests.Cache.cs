namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    [Fact(DisplayName = "GetAllAsync caches results when cache is enabled and allowed")]
    public async Task GetAllAsync_Caches_WhenEnabled()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        counter.Reset();
        var first = await repo.GetAllAsync();
        first.Count().ShouldBe(3);
        cache.Count.ShouldBe(1);
        counter.Count.ShouldBeGreaterThan(0);

        counter.Reset();
        var second = await repo.GetAllAsync();
        second.Count().ShouldBe(3);
        cache.Count.ShouldBe(1);
        counter.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllAsync does not cache when filter is provided")]
    public async Task GetAllAsync_DoesNotCache_WithFilter()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        var items = await repo.GetAllAsync(p => p.Price > 0m);
        items.Count().ShouldBe(3);
        cache.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllAsync does not cache when includes are provided")]
    public async Task GetAllAsync_DoesNotCache_WithIncludes()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        var items = await repo.GetAllAsync(
            includeProperties: ["Reviews"],
            cancellationToken: default);

        items.Count().ShouldBe(3);
        cache.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllAsync does not cache when global filter is set")]
    public async Task GetAllAsync_DoesNotCache_WithGlobalFilter()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllAsync
        }.AddGlobalWhereFilter<Product>(p => p.Price > 0m);

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        var items = await repo.GetAllAsync();
        items.Count().ShouldBe(3);
        cache.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllAsync does not cache when orderBy is provided")]
    public async Task GetAllAsync_DoesNotCache_WithOrderBy()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        var items = await repo.GetAllAsync(orderBy: q => q.OrderBy(p => p.Price));
        items.Count().ShouldBe(3);
        cache.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllAsync does not cache when includeGraph is provided")]
    public async Task GetAllAsync_DoesNotCache_WithIncludeGraph()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: null,
            includeGraph: new IncludeGraph<Product>(x => x.Reviews),
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        items.Count().ShouldBe(3);
        cache.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllAsync does not cache when includeExpressions are provided")]
    public async Task GetAllAsync_DoesNotCache_WithIncludeExpressions()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            asNoTracking: true,
            useSplitQuery: null,
            cancellationToken: default,
            includeExpressions: static p => p.Reviews);

        items.Count().ShouldBe(3);
        cache.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllAsync cache key varies by tenant scope")]
    public async Task GetAllAsync_CacheKey_UsesTenantScope()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllAsync
        };

        var customFactory = WithPolicy(policy);
        using var client = customFactory.CreateClient();
        using var scope = customFactory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        static HttpRequestMessage BuildRequest(string tenant)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/product");
            request.Headers.Add("X-Tenant-Id", tenant);
            return request;
        }

        var responseA = await client.SendAsync(BuildRequest("tenant-a"));
        responseA.EnsureSuccessStatusCode();
        cache.Count.ShouldBe(1);

        var responseB = await client.SendAsync(BuildRequest("tenant-b"));
        responseB.EnsureSuccessStatusCode();
        cache.Count.ShouldBe(2);
    }
}
