namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    [Fact(DisplayName = "GetAllIncludingDeletedAsync caches results when enabled and allowed")]
    public async Task GetAllIncludingDeletedAsync_Caches_WhenEnabled()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllIncludingDeletedAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        counter.Reset();
        var first = await repo.GetAllIncludingDeletedAsync();
        first.Count.ShouldBe(3);
        cache.Count.ShouldBe(1);
        counter.Count.ShouldBeGreaterThan(0);

        counter.Reset();
        var second = await repo.GetAllIncludingDeletedAsync();
        second.Count.ShouldBe(3);
        cache.Count.ShouldBe(1);
        counter.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync does not cache when filter is provided")]
    public async Task GetAllIncludingDeletedAsync_DoesNotCache_WithFilter()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllIncludingDeletedAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        var items = await repo.GetAllIncludingDeletedAsync(p => p.Price > 0m);
        items.Count.ShouldBe(3);
        cache.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync does not cache when includes are provided")]
    public async Task GetAllIncludingDeletedAsync_DoesNotCache_WithIncludes()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllIncludingDeletedAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        var items = await repo.GetAllIncludingDeletedAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        items.Count.ShouldBe(3);
        cache.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync does not cache when orderBy is provided")]
    public async Task GetAllIncludingDeletedAsync_DoesNotCache_WithOrderBy()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllIncludingDeletedAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        var items = await repo.GetAllIncludingDeletedAsync(orderBy: q => q.OrderBy(p => p.Price));
        items.Count.ShouldBe(3);
        cache.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync does not cache when includeGraph is provided")]
    public async Task GetAllIncludingDeletedAsync_DoesNotCache_WithIncludeGraph()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllIncludingDeletedAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        var items = await repo.GetAllIncludingDeletedAsync(
            filter: null,
            orderBy: null,
            includeProperties: null,
            includeGraph: new IncludeGraph<Product>(x => x.Reviews),
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        items.Count.ShouldBe(3);
        cache.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync does not cache when includeExpressions are provided")]
    public async Task GetAllIncludingDeletedAsync_DoesNotCache_WithIncludeExpressions()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllIncludingDeletedAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        var items = await repo.GetAllIncludingDeletedAsync(
            filter: null,
            orderBy: null,
            asNoTracking: true,
            useSplitQuery: null,
            cancellationToken: default,
            p => p.Reviews);

        items.Count.ShouldBe(3);
        cache.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync does not cache when read operations are not allowed")]
    public async Task GetAllIncludingDeletedAsync_DoesNotCache_WhenReadOpsDisallow()
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

        var items = await repo.GetAllIncludingDeletedAsync();
        items.Count.ShouldBe(3);
        cache.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync cache key varies by tenant scope")]
    public async Task GetAllIncludingDeletedAsync_CacheKey_UsesTenantScope()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllIncludingDeletedAsync
        };

        var customFactory = WithPolicy(policy).WithWebHostBuilder(builder =>
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
        using (var scopeB = customFactory.Services.CreateScope())
        {
            var repoB = scopeB.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var itemsB = await repoB.GetAllIncludingDeletedAsync();
            itemsB.Count.ShouldBe(3);
            cache.Count.ShouldBe(2);
        }
    }
}
