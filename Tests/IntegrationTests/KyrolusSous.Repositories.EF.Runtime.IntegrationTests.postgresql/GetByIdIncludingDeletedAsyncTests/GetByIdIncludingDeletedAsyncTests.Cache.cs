namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdIncludingDeletedAsyncTests;

public partial class GetByIdIncludingDeletedAsyncTests
{
    private static readonly KyrolusRepositoryPolicy CachePolicy = new()
    {
        DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
        DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdIncludingDeletedAsync
    };

    public static TheoryData<string, bool> CacheEntityCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "GetByIdIncludingDeletedAsync caches results when enabled and allowed")]
    [MemberData(nameof(CacheEntityCases))]
    public async Task GetByIdIncludingDeletedAsync_Caches_WhenEnabled(string caseId, bool isComposite)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (isComposite)
        {
            await WithReviewSoftDeleted(async (repo, sp) =>
            {
                var cache = sp.GetRequiredService<InMemoryCacheProvider>();
                var counter = sp.GetRequiredService<CommandCounterInterceptor>();
                cache.Clear();
                cache.Count.ShouldBe(0);

                counter.Reset();
                (await repo.GetByIdIncludingDeletedAsync(ExistingReviewKey)).ShouldNotBeNull();
                cache.Count.ShouldBe(1);
                counter.Count.ShouldBeGreaterThan(0);

                counter.Reset();
                (await repo.GetByIdIncludingDeletedAsync(ExistingReviewKey)).ShouldNotBeNull();
                cache.Count.ShouldBe(1);
                counter.Count.ShouldBe(0);
            }, CachePolicy);
            return;
        }

        await WithProductSoftDeleted(async (repo, sp) =>
        {
            var cache = sp.GetRequiredService<InMemoryCacheProvider>();
            var counter = sp.GetRequiredService<CommandCounterInterceptor>();
            cache.Clear();
            cache.Count.ShouldBe(0);

            counter.Reset();
            (await repo.GetByIdIncludingDeletedAsync(ExistingDeletedProductId)).ShouldNotBeNull();
            cache.Count.ShouldBe(1);
            counter.Count.ShouldBeGreaterThan(0);

            counter.Reset();
            (await repo.GetByIdIncludingDeletedAsync(ExistingDeletedProductId)).ShouldNotBeNull();
            cache.Count.ShouldBe(1);
            counter.Count.ShouldBe(0);
        }, CachePolicy);
    }

    [Theory(DisplayName = "GetByIdIncludingDeletedAsync does not cache when includes are provided")]
    [MemberData(nameof(CacheEntityCases))]
    public async Task GetByIdIncludingDeletedAsync_DoesNotCache_WithIncludes(string caseId, bool isComposite)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (isComposite)
        {
            await WithReviewSoftDeleted(async (repo, sp) =>
            {
                var cache = sp.GetRequiredService<InMemoryCacheProvider>();
                cache.Clear();
                cache.Count.ShouldBe(0);

                _ = await repo.GetByIdIncludingDeletedAsync(
                    ExistingReviewKey,
                    includeProperties: ["Product"],
                    includeGraph: null,
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default);

                cache.Count.ShouldBe(0);
            }, CachePolicy);
            return;
        }

        await WithProductSoftDeleted(async (repo, sp) =>
        {
            var cache = sp.GetRequiredService<InMemoryCacheProvider>();
            cache.Clear();
            cache.Count.ShouldBe(0);

            _ = await repo.GetByIdIncludingDeletedAsync(
                ExistingDeletedProductId,
                includeProperties: ["Store"],
                includeGraph: null,
                asNoTracking: true,
                useSplitQuery: null,
                cancellationToken: default);

            cache.Count.ShouldBe(0);
        }, CachePolicy);
    }

    [Theory(DisplayName = "GetByIdIncludingDeletedAsync does not cache when include graph is provided")]
    [MemberData(nameof(CacheEntityCases))]
    public async Task GetByIdIncludingDeletedAsync_DoesNotCache_WithIncludeGraph(string caseId, bool isComposite)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (isComposite)
        {
            await WithReviewSoftDeleted(async (repo, sp) =>
            {
                var cache = sp.GetRequiredService<InMemoryCacheProvider>();
                cache.Clear();
                cache.Count.ShouldBe(0);

                _ = await repo.GetByIdIncludingDeletedAsync(
                    ExistingReviewKey,
                    includeProperties: null,
                    includeGraph: new IncludeGraph<Review>(x => x.Product),
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default);

                cache.Count.ShouldBe(0);
            }, CachePolicy);
            return;
        }

        await WithProductSoftDeleted(async (repo, sp) =>
        {
            var cache = sp.GetRequiredService<InMemoryCacheProvider>();
            cache.Clear();
            cache.Count.ShouldBe(0);

            _ = await repo.GetByIdIncludingDeletedAsync(
                ExistingDeletedProductId,
                includeProperties: null,
                includeGraph: new IncludeGraph<Product>(x => x.Reviews),
                asNoTracking: true,
                useSplitQuery: null,
                cancellationToken: default);

            cache.Count.ShouldBe(0);
        }, CachePolicy);
    }

    [Theory(DisplayName = "GetByIdIncludingDeletedAsync does not cache when include expressions are provided")]
    [MemberData(nameof(CacheEntityCases))]
    public async Task GetByIdIncludingDeletedAsync_DoesNotCache_WithIncludeExpressions(string caseId, bool isComposite)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (isComposite)
        {
            await WithReviewSoftDeleted(async (repo, sp) =>
            {
                var cache = sp.GetRequiredService<InMemoryCacheProvider>();
                cache.Clear();
                cache.Count.ShouldBe(0);

                _ = await repo.GetByIdIncludingDeletedAsync(
                    ExistingReviewKey,
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default,
                    x => x.Product);

                cache.Count.ShouldBe(0);
            }, CachePolicy);
            return;
        }

        await WithProductSoftDeleted(async (repo, sp) =>
        {
            var cache = sp.GetRequiredService<InMemoryCacheProvider>();
            cache.Clear();
            cache.Count.ShouldBe(0);

            _ = await repo.GetByIdIncludingDeletedAsync(
                ExistingDeletedProductId,
                asNoTracking: true,
                useSplitQuery: null,
                cancellationToken: default,
                x => x.Store);

            cache.Count.ShouldBe(0);
        }, CachePolicy);
    }

    [Theory(DisplayName = "GetByIdIncludingDeletedAsync does not cache when read operation is not allowed")]
    [MemberData(nameof(CacheEntityCases))]
    public async Task GetByIdIncludingDeletedAsync_DoesNotCache_WhenReadOpsDisallow(string caseId, bool isComposite)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.None
        };

        if (isComposite)
        {
            await WithReviewSoftDeleted(async (repo, sp) =>
            {
                var cache = sp.GetRequiredService<InMemoryCacheProvider>();
                cache.Clear();
                cache.Count.ShouldBe(0);

                _ = await repo.GetByIdIncludingDeletedAsync(ExistingReviewKey);
                cache.Count.ShouldBe(0);
            }, policy);
            return;
        }

        await WithProductSoftDeleted(async (repo, sp) =>
        {
            var cache = sp.GetRequiredService<InMemoryCacheProvider>();
            cache.Clear();
            cache.Count.ShouldBe(0);

            _ = await repo.GetByIdIncludingDeletedAsync(ExistingDeletedProductId);
            cache.Count.ShouldBe(0);
        }, policy);
    }

    [Fact(DisplayName = "GetByIdIncludingDeletedAsync cache key differs from GetByIdAsync key")]
    public async Task GetByIdIncludingDeletedAsync_CacheKey_Differs_From_GetByIdAsync()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdAsync | KyrolusCacheReadOperations.GetByIdIncludingDeletedAsync
        };

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();

        cache.Clear();
        cache.Count.ShouldBe(0);

        _ = await repo.GetByIdAsync(ExistingProductId);
        cache.Count.ShouldBe(1);

        _ = await repo.GetByIdIncludingDeletedAsync(ExistingProductId);
        cache.Count.ShouldBe(2);
    }

    [Fact(DisplayName = "GetByIdIncludingDeletedAsync cache key varies by tenant scope")]
    public async Task GetByIdIncludingDeletedAsync_CacheKey_UsesTenantScope()
    {
        var customFactory = WithPolicy(CachePolicy).WithWebHostBuilder(builder =>
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
            (await repoA.GetByIdIncludingDeletedAsync(ExistingProductId)).ShouldNotBeNull();
            cache.Count.ShouldBe(1);
        }

        TenantScope.Value = "tenant-b";
        using (var scopeB = customFactory.Services.CreateScope())
        {
            var repoB = scopeB.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            (await repoB.GetByIdIncludingDeletedAsync(ExistingProductId)).ShouldNotBeNull();
            cache.Count.ShouldBe(2);
        }

        TenantScope.Value = null;
    }
}
