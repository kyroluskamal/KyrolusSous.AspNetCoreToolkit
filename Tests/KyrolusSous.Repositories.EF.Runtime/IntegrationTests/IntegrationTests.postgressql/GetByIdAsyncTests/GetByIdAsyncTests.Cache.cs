namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    private sealed record CacheSpec(Func<GetByIdAsyncTests, Task> Run);

    private static readonly IReadOnlyDictionary<string, CacheSpec> CacheSpecs = BuildCacheSpecs();

    public static TheoryData<string> CacheCases => CaseIdsFrom(CacheSpecs);

    [Theory(DisplayName = "GetByIdAsync cache behavior")]
    [MemberData(nameof(CacheCases))]
    public Task GetByIdAsync_Cache_Behavior(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return CacheSpecs[caseId].Run(this);
    }

    private static IReadOnlyDictionary<string, CacheSpec> BuildCacheSpecs()
        => new Dictionary<string, CacheSpec>
        {
            ["enabled-single"] = new CacheSpec(async test =>
            {
                var policy = new KyrolusRepositoryPolicy
                {
                    DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
                    DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdAsync
                };

                var customFactory = test.WithPolicy(policy);
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
            }),
            ["enabled-composite"] = new CacheSpec(async test =>
            {
                var policy = new KyrolusRepositoryPolicy
                {
                    DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
                    DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdAsync
                };

                var customFactory = test.WithPolicy(policy);
                using var scope = customFactory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, Review>>();
                var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();
                var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

                cache.Clear();
                cache.Count.ShouldBe(0);

                counter.Reset();
                var first = await repo.GetByIdAsync(CompositeKey_ProductReview);
                first.ShouldNotBeNull();
                cache.Count.ShouldBe(1);
                counter.Count.ShouldBeGreaterThan(0);

                counter.Reset();
                var second = await repo.GetByIdAsync(CompositeKey_ProductReview);
                second.ShouldNotBeNull();
                cache.Count.ShouldBe(1);
                counter.Count.ShouldBe(0);
            }),
            ["no-cache-includes"] = new CacheSpec(async test =>
            {
                var policy = new KyrolusRepositoryPolicy
                {
                    DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
                    DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdAsync
                };

                var customFactory = test.WithPolicy(policy);
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
            }),
            ["no-cache-include-graph"] = new CacheSpec(async test =>
            {
                var policy = new KyrolusRepositoryPolicy
                {
                    DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
                    DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdAsync
                };

                var customFactory = test.WithPolicy(policy);
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
            }),
            ["cache-include-expressions"] = new CacheSpec(async test =>
            {
                var policy = new KyrolusRepositoryPolicy
                {
                    DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
                    DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdAsync
                };

                var customFactory = test.WithPolicy(policy);
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
            }),
            ["no-cache-read-ops"] = new CacheSpec(async test =>
            {
                var policy = new KyrolusRepositoryPolicy
                {
                    DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
                    DefaultCacheReadOperations = KyrolusCacheReadOperations.None
                };

                var customFactory = test.WithPolicy(policy);
                using var scope = customFactory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
                var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

                cache.Clear();
                cache.Count.ShouldBe(0);

                var item = await repo.GetByIdAsync(Guid.Parse(productLaptopId));
                item.ShouldNotBeNull();
                cache.Count.ShouldBe(0);
            }),
            ["tenant-scope"] = new CacheSpec(async test =>
            {
                var policy = new KyrolusRepositoryPolicy
                {
                    DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
                    DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdAsync
                };

                var customFactory = test.WithPolicy(policy);
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
            })
        };
}
