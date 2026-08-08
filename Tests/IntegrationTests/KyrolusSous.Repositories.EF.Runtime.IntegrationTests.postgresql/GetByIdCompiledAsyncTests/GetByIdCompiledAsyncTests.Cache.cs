namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdCompiledAsyncTests;

public partial class GetByIdCompiledAsyncTests
{
    [Fact(DisplayName = "GetByIdCompiledAsync caches results when cache is enabled and allowed")]
    public async Task GetByIdCompiledAsync_Caches_WhenEnabled()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdCompiledAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        counter.Reset();
        (await repo.GetByIdCompiledAsync(ExistingProductId)).ShouldNotBeNull();
        cache.Count.ShouldBe(1);
        counter.Count.ShouldBeGreaterThan(0);

        counter.Reset();
        (await repo.GetByIdCompiledAsync(ExistingProductId)).ShouldNotBeNull();
        cache.Count.ShouldBe(1);
        counter.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetByIdCompiledAsync bypasses cache when read operation is not allowed")]
    public async Task GetByIdCompiledAsync_BypassesCache_WhenReadOperationIsNotAllowed()
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
        (await repo.GetByIdCompiledAsync(ExistingProductId)).ShouldNotBeNull();
        cache.Count.ShouldBe(0);
        counter.Count.ShouldBeGreaterThan(0);
    }

    [Fact(DisplayName = "GetByIdCompiledAsync cache key varies by tenant scope")]
    public async Task GetByIdCompiledAsync_CacheKey_Varies_ByTenant()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdCompiledAsync
        };

        var customFactory = WithPolicy(policy);
        using var client = customFactory.CreateClient();
        using var scope = customFactory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        static HttpRequestMessage BuildRequest(string tenant)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/product/{DataSeeder.productLaptopId}/compiled");
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
