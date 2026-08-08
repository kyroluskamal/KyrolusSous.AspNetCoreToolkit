using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.ExecuteUpdateAsyncTests;

public partial class ExecuteUpdateAsyncTests
{
    private sealed record SingleRepositorySpec(
        Func<List<Product>> Seed,
        Func<List<Product>, Expression<Func<Product, bool>>?> FilterFactory,
        Action<UpdateSettersBuilder<Product>> Setters,
        Action<List<Product>, List<Product>, int> AssertPersisted);

    private sealed record CompositeRepositorySpec(
        Func<List<Review>> Seed,
        Func<List<Review>, Expression<Func<Review, bool>>?> FilterFactory,
        Action<UpdateSettersBuilder<Review>> Setters,
        Action<List<Review>, List<Review>, int> AssertPersisted);

    private sealed record SingleApiSpec(
        Func<Product> Seed,
        Func<Product, QueryRequest?> BuildRequest,
        List<PropertyUpdateRequest> Updates,
        int ExpectedAffected,
        Action<Product, Product> AssertPersisted);

    private sealed record CompositeApiSpec(
        Func<Review> Seed,
        Func<Review, QueryRequest?> BuildRequest,
        List<PropertyUpdateRequest> Updates,
        int ExpectedAffected,
        Action<Review, Review> AssertPersisted);

    private sealed record GlobalFilterSpec(bool IsComposite, KyrolusRepositoryPolicy Policy, int ExpectedAffected);

    private static readonly IReadOnlyDictionary<string, SingleRepositorySpec> SingleRepositorySpecs = BuildSingleRepositorySpecs();
    private static readonly IReadOnlyDictionary<string, CompositeRepositorySpec> CompositeRepositorySpecs = BuildCompositeRepositorySpecs();
    private static readonly IReadOnlyDictionary<string, SingleApiSpec> SingleApiSpecs = BuildSingleApiSpecs();
    private static readonly IReadOnlyDictionary<string, CompositeApiSpec> CompositeApiSpecs = BuildCompositeApiSpecs();
    private static readonly IReadOnlyDictionary<string, GlobalFilterSpec> GlobalFilterSpecs = BuildGlobalFilterSpecs();

    public static TheoryData<string> SingleRepositoryCases => CaseIdsFrom(SingleRepositorySpecs);
    public static TheoryData<string> CompositeRepositoryCases => CaseIdsFrom(CompositeRepositorySpecs);
    public static TheoryData<string> SingleApiCases => CaseIdsFrom(SingleApiSpecs);
    public static TheoryData<string> CompositeApiCases => CaseIdsFrom(CompositeApiSpecs);
    public static TheoryData<string> GlobalFilterCases => CaseIdsFrom(GlobalFilterSpecs);

    public static TheoryData<string, bool?, bool> UseSplitCases => new()
    {
        { "explicit-false-overrides-policy-true", false, true },
        { "explicit-true-overrides-policy-false", true, false },
        { "null-uses-policy-true", null, true },
        { "null-uses-policy-false", null, false }
    };

    [Theory(DisplayName = "ExecuteUpdateAsync updates single-key entities via repository")]
    [MemberData(nameof(SingleRepositoryCases))]
    public async Task ExecuteUpdateAsync_SingleKey_Repository_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SingleRepositorySpecs[caseId];
        var seed = spec.Seed();
        await SeedProductsAsync(seed);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

            var affected = await repo.ExecuteUpdateAsync(spec.FilterFactory(seed), spec.Setters, useSplitQuery: false);
            var ids = seed.Select(x => x.Id).ToArray();
            var persisted = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Products.AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .OrderBy(x => x.Id)
                .ToListAsync();

            spec.AssertPersisted(seed, persisted, affected);
        }
        finally
        {
            await CleanupProductsAsync(seed.Select(x => x.Id));
        }
    }

    [Theory(DisplayName = "ExecuteUpdateAsync updates composite-key entities via repository")]
    [MemberData(nameof(CompositeRepositoryCases))]
    public async Task ExecuteUpdateAsync_CompositeKey_Repository_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = CompositeRepositorySpecs[caseId];
        var seed = spec.Seed();
        await SeedReviewsAsync(seed);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

            var affected = await repo.ExecuteUpdateAsync(spec.FilterFactory(seed), spec.Setters, useSplitQuery: false);
            var keys = seed.Select(x => (x.ProductId, x.CustomerId)).ToArray();
            var persisted = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Reviews.AsNoTracking()
                .Where(x => keys.Select(k => k.ProductId).Contains(x.ProductId) && keys.Select(k => k.CustomerId).Contains(x.CustomerId))
                .ToListAsync();

            persisted = persisted
                .Where(x => keys.Any(k => k.ProductId == x.ProductId && k.CustomerId == x.CustomerId))
                .OrderBy(x => x.ProductId)
                .ThenBy(x => x.CustomerId)
                .ToList();

            spec.AssertPersisted(seed, persisted, affected);
        }
        finally
        {
            await CleanupReviewsAsync(seed.Select(x => (x.ProductId, x.CustomerId)));
        }
    }

    [Fact(DisplayName = "ExecuteUpdateAsync supports null filter and updates all visible rows")]
    public async Task ExecuteUpdateAsync_NullFilter_UpdatesAllVisibleRows()
    {
        var customFactory = WithPolicy();
        using var scope = customFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var before = await db.Products.CountAsync(x => !x.IsDeleted);
        var affected = await repo.ExecuteUpdateAsync(
            filter: null,
            setPropertyCalls: setters => setters.SetProperty(x => x.Price, x => x.Price),
            useSplitQuery: false);

        affected.ShouldBe(before);
    }

    [Theory(DisplayName = "ExecuteUpdateAsync respects split query option for single-key entities")]
    [MemberData(nameof(UseSplitCases))]
    public async Task ExecuteUpdateAsync_SingleKey_SplitQueryOption_Works(string caseId, bool? useSplitQuery, bool policyDefault)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var policy = new KyrolusRepositoryPolicy { UseSplitQueryDefault = policyDefault };
        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var name = $"split-single-{caseId}";

        var affected = await repo.ExecuteUpdateAsync(
            x => x.Id == DataSeeder.productLaptopId,
            setters => setters.SetProperty(x => x.Name, x => name),
            useSplitQuery: useSplitQuery);

        affected.ShouldBe(1);
        var persisted = await db.Products.AsNoTracking().SingleAsync(x => x.Id == DataSeeder.productLaptopId);
        persisted.Name.ShouldBe(name);
    }

    [Theory(DisplayName = "ExecuteUpdateAsync respects split query option for composite-key entities")]
    [MemberData(nameof(UseSplitCases))]
    public async Task ExecuteUpdateAsync_CompositeKey_SplitQueryOption_Works(string caseId, bool? useSplitQuery, bool policyDefault)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var policy = new KyrolusRepositoryPolicy { UseSplitQueryDefault = policyDefault };
        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var comment = $"split-composite-{caseId}";

        var affected = await repo.ExecuteUpdateAsync(
            x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId,
            setters => setters.SetProperty(x => x.Comment, x => comment),
            useSplitQuery: useSplitQuery);

        affected.ShouldBe(1);
        var persisted = await db.Reviews.AsNoTracking()
            .SingleAsync(x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId);
        persisted.Comment.ShouldBe(comment);
    }

    [Fact(DisplayName = "ExecuteUpdateAsync excludes soft-deleted single-key entities")]
    public async Task ExecuteUpdateAsync_SingleKey_SoftDeletedEntity_IsExcluded()
    {
        var entity = CreateValidProduct(name: "exec-soft-single-before");
        await SeedProductAsync(entity);

        try
        {
            await SoftDeleteProductAsync(entity.Id);
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

            var affected = await repo.ExecuteUpdateAsync(
                x => x.Id == entity.Id,
                setters => setters.SetProperty(x => x.Name, x => "exec-soft-single-after"),
                useSplitQuery: false);

            affected.ShouldBe(0);
            var persisted = await FindProductAsync(entity.Id, ignoreFilters: true);
            persisted.ShouldNotBeNull();
            persisted!.IsDeleted.ShouldBeTrue();
            persisted.Name.ShouldBe("exec-soft-single-before");
        }
        finally
        {
            await CleanupProductAsync(entity.Id);
        }
    }

    [Fact(DisplayName = "ExecuteUpdateAsync excludes soft-deleted composite-key entities")]
    public async Task ExecuteUpdateAsync_CompositeKey_SoftDeletedEntity_IsExcluded()
    {
        var entity = CreateValidReview(
            productId: DataSeeder.productBookId,
            customerId: DataSeeder.customerJohnId,
            rating: 3,
            comment: "exec-soft-composite-before");
        await SeedReviewAsync(entity);

        try
        {
            await SoftDeleteReviewAsync(entity.ProductId, entity.CustomerId);
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

            var affected = await repo.ExecuteUpdateAsync(
                x => x.ProductId == entity.ProductId && x.CustomerId == entity.CustomerId,
                setters => setters.SetProperty(x => x.Comment, x => "exec-soft-composite-after"),
                useSplitQuery: false);

            affected.ShouldBe(0);
            var persisted = await FindReviewAsync(entity.ProductId, entity.CustomerId, ignoreFilters: true);
            persisted.ShouldNotBeNull();
            persisted!.IsDeleted.ShouldBeTrue();
            persisted.Comment.ShouldBe("exec-soft-composite-before");
        }
        finally
        {
            await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
        }
    }

    [Theory(DisplayName = "ExecuteUpdateAsync respects global filters")]
    [MemberData(nameof(GlobalFilterCases))]
    public async Task ExecuteUpdateAsync_GlobalFilters_Work(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = GlobalFilterSpecs[caseId];
        var customFactory = WithPolicy(spec.Policy);
        using var scope = customFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (spec.IsComposite)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var affected = await repo.ExecuteUpdateAsync(
                x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId,
                setters => setters.SetProperty(x => x.Comment, x => $"global-{caseId}"),
                useSplitQuery: false);

            affected.ShouldBe(spec.ExpectedAffected);
            var persisted = await db.Reviews.AsNoTracking()
                .SingleAsync(x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId);

            if (spec.ExpectedAffected == 0)
                persisted.Comment.ShouldBe("Great laptop, fast shipping.");
            else
                persisted.Comment.ShouldBe($"global-{caseId}");
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleAffected = await singleRepo.ExecuteUpdateAsync(
            x => x.Id == DataSeeder.productLaptopId,
            setters => setters.SetProperty(x => x.Name, x => $"global-{caseId}"),
            useSplitQuery: false);

        singleAffected.ShouldBe(spec.ExpectedAffected);
        var singlePersisted = await db.Products.AsNoTracking().SingleAsync(x => x.Id == DataSeeder.productLaptopId);

        if (spec.ExpectedAffected == 0)
            singlePersisted.Name.ShouldBe("Laptop Pro 15");
        else
            singlePersisted.Name.ShouldBe($"global-{caseId}");
    }

    [Fact(DisplayName = "ExecuteUpdateAsync invalidates GetAllAsync cache entries")]
    public async Task ExecuteUpdateAsync_Invalidates_GetAllAsync_Cache()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = new KyrolusCachePolicy(Enabled: true),
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetAllAsync
        };
        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var cache = scope.ServiceProvider.GetRequiredService<InMemoryCacheProvider>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        var entity = CreateValidProduct(name: "cache-before", sku: $"EXU-CACHE-{Guid.NewGuid():N}");
        db.Products.Add(entity);
        await db.SaveChangesAsync();

        cache.Clear();
        cache.Count.ShouldBe(0);

        counter.Reset();
        var first = (await repo.GetAllAsync()).ToList();
        first.Any(x => x.Id == entity.Id).ShouldBeTrue();
        cache.Count.ShouldBe(1);
        counter.Count.ShouldBeGreaterThan(0);

        counter.Reset();
        _ = (await repo.GetAllAsync()).ToList();
        counter.Count.ShouldBe(0);

        var affected = await repo.ExecuteUpdateAsync(
            x => x.Id == entity.Id,
            setters => setters.SetProperty(x => x.Name, x => "cache-after"),
            useSplitQuery: false);
        affected.ShouldBe(1);

        counter.Reset();
        var afterUpdate = (await repo.GetAllAsync()).ToList();
        counter.Count.ShouldBeGreaterThan(0);
        afterUpdate.Single(x => x.Id == entity.Id).Name.ShouldBe("cache-after");
    }

    [Fact(DisplayName = "ExecuteUpdateAsync uses bulk executor when available")]
    public async Task ExecuteUpdateAsync_UsesBulkExecutor_WhenRegistered()
    {
        var customFactory = WithPolicy().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped<RecordingProductBulkExecutor>();
                services.AddScoped<IKyrolusBulkExecutor<Product>>(sp => sp.GetRequiredService<RecordingProductBulkExecutor>());
            });
        });

        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var recorder = scope.ServiceProvider.GetRequiredService<RecordingProductBulkExecutor>();

        var affected = await repo.ExecuteUpdateAsync(
            x => x.Id == DataSeeder.productLaptopId,
            setters => setters.SetProperty(x => x.Name, x => "bulk-path-name"),
            useSplitQuery: true);

        affected.ShouldBe(recorder.ReturnValue);
        recorder.ExecuteUpdateCalls.ShouldBe(1);
        recorder.LastUseSplitQuery.ShouldBe(true);
        recorder.LastFilter.ShouldNotBeNull();

        var persisted = await db.Products.AsNoTracking().SingleAsync(x => x.Id == DataSeeder.productLaptopId);
        persisted.Name.ShouldBe("Laptop Pro 15");
    }

    [Theory(DisplayName = "ExecuteUpdate API updates single-key entities")]
    [MemberData(nameof(SingleApiCases))]
    public async Task ExecuteUpdateAsync_Api_SingleKey_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SingleApiSpecs[caseId];
        var entity = spec.Seed();
        await SeedProductAsync(entity);

        try
        {
            var (response, affected, content) = await PostExecuteUpdateAsync<Product>(spec.BuildRequest(entity), spec.Updates);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, content);
            affected.ShouldNotBeNull();
            affected!.Value.ShouldBe(spec.ExpectedAffected);

            var persisted = await FindProductAsync(entity.Id, ignoreFilters: true);
            persisted.ShouldNotBeNull();
            spec.AssertPersisted(entity, persisted!);
        }
        finally
        {
            await CleanupProductAsync(entity.Id);
        }
    }

    [Theory(DisplayName = "ExecuteUpdate API updates composite-key entities")]
    [MemberData(nameof(CompositeApiCases))]
    public async Task ExecuteUpdateAsync_Api_CompositeKey_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = CompositeApiSpecs[caseId];
        var entity = spec.Seed();
        await SeedReviewAsync(entity);

        try
        {
            var (response, affected, content) = await PostExecuteUpdateAsync<Review>(spec.BuildRequest(entity), spec.Updates);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, content);
            affected.ShouldNotBeNull();
            affected!.Value.ShouldBe(spec.ExpectedAffected);

            var persisted = await FindReviewAsync(entity.ProductId, entity.CustomerId, ignoreFilters: true);
            persisted.ShouldNotBeNull();
            spec.AssertPersisted(entity, persisted!);
        }
        finally
        {
            await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
        }
    }

    [Fact(DisplayName = "ExecuteUpdate API excludes soft-deleted single-key entities")]
    public async Task ExecuteUpdateAsync_Api_SingleKey_SoftDeletedEntity_IsExcluded()
    {
        var entity = CreateValidProduct(name: "api-soft-single-before");
        await SeedProductAsync(entity);

        try
        {
            await SoftDeleteProductAsync(entity.Id);
            var request = new QueryRequest(Filters:
            [
                new FilterClause("Id", "eq", entity.Id.ToString())
            ]);
            var updates = new List<PropertyUpdateRequest>
            {
                new("Weight", null)
            };

            var (response, affected, content) = await PostExecuteUpdateAsync<Product>(request, updates);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, content);
            affected.ShouldNotBeNull();
            affected!.Value.ShouldBe(0);

            var persisted = await FindProductAsync(entity.Id, ignoreFilters: true);
            persisted.ShouldNotBeNull();
            persisted!.Name.ShouldBe("api-soft-single-before");
        }
        finally
        {
            await CleanupProductAsync(entity.Id);
        }
    }

    [Fact(DisplayName = "ExecuteUpdate API excludes soft-deleted composite-key entities")]
    public async Task ExecuteUpdateAsync_Api_CompositeKey_SoftDeletedEntity_IsExcluded()
    {
        var entity = CreateValidReview(
            productId: DataSeeder.productBookId,
            customerId: DataSeeder.customerJohnId,
            rating: 3,
            comment: "api-soft-composite-before");
        await SeedReviewAsync(entity);

        try
        {
            await SoftDeleteReviewAsync(entity.ProductId, entity.CustomerId);
            var request = new QueryRequest(Filters:
            [
                new FilterClause("ProductId", "eq", entity.ProductId.ToString()),
                new FilterClause("CustomerId", "eq", entity.CustomerId.ToString())
            ]);
            var updates = new List<PropertyUpdateRequest>
            {
                new("Comment", null)
            };

            var (response, affected, content) = await PostExecuteUpdateAsync<Review>(request, updates);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, content);
            affected.ShouldNotBeNull();
            affected!.Value.ShouldBe(0);

            var persisted = await FindReviewAsync(entity.ProductId, entity.CustomerId, ignoreFilters: true);
            persisted.ShouldNotBeNull();
            persisted!.Comment.ShouldBe("api-soft-composite-before");
        }
        finally
        {
            await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
        }
    }

    private static IReadOnlyDictionary<string, SingleRepositorySpec> BuildSingleRepositorySpecs()
        => new Dictionary<string, SingleRepositorySpec>
        {
            ["update-single-row"] = new(
                Seed: () =>
                [
                    CreateValidProduct(name: "repo-single-a", sku: $"EXU-SN-A-{Guid.NewGuid():N}", stockQuantity: 5, count: 2),
                    CreateValidProduct(name: "repo-single-b", sku: $"EXU-SN-B-{Guid.NewGuid():N}", stockQuantity: 8, count: 3)
                ],
                FilterFactory: entities => x => x.Id == entities[0].Id,
                Setters: setters => setters
                    .SetProperty(x => x.Name, x => "repo-single-updated")
                    .SetProperty(x => x.StockQuantity, x => x.StockQuantity + 10),
                AssertPersisted: (seed, persisted, affected) =>
                {
                    affected.ShouldBe(1);
                    persisted.Count.ShouldBe(2);

                    var first = persisted.Single(x => x.Id == seed[0].Id);
                    var second = persisted.Single(x => x.Id == seed[1].Id);
                    first.Name.ShouldBe("repo-single-updated");
                    first.StockQuantity.ShouldBe(seed[0].StockQuantity + 10);
                    second.Name.ShouldBe(seed[1].Name);
                    second.StockQuantity.ShouldBe(seed[1].StockQuantity);
                }),

            ["update-multiple-nullables"] = new(
                Seed: () =>
                {
                    var prefix = $"EXU-SN-MULTI-{Guid.NewGuid():N}";
                    return
                    [
                        CreateValidProduct(name: "repo-multi-a", sku: $"{prefix}-1", count: 5, weight: 1.5m),
                        CreateValidProduct(name: "repo-multi-b", sku: $"{prefix}-2", count: 6, weight: 2.5m)
                    ];
                },
                FilterFactory: entities =>
                {
                    var prefix = entities[0].Sku[..^2];
                    return x => x.Sku.StartsWith(prefix);
                },
                Setters: setters => setters
                    .SetProperty(x => x.Count, x => (int?)null)
                    .SetProperty(x => x.Weight, x => (decimal?)null),
                AssertPersisted: (_, persisted, affected) =>
                {
                    affected.ShouldBe(2);
                    persisted.Count.ShouldBe(2);
                    persisted.All(x => x.Count is null).ShouldBeTrue();
                    persisted.All(x => x.Weight is null).ShouldBeTrue();
                }),

            ["no-match"] = new(
                Seed: () =>
                [
                    CreateValidProduct(name: "repo-no-match", sku: $"EXU-SN-NM-{Guid.NewGuid():N}", price: 42m)
                ],
                FilterFactory: _ =>
                {
                    var missingId = Guid.NewGuid();
                    return x => x.Id == missingId;
                },
                Setters: setters => setters.SetProperty(x => x.Name, x => "repo-no-match-updated"),
                AssertPersisted: (seed, persisted, affected) =>
                {
                    affected.ShouldBe(0);
                    persisted.Count.ShouldBe(1);
                    persisted[0].Name.ShouldBe(seed[0].Name);
                    persisted[0].Price.ShouldBe(seed[0].Price);
                })
        };

    private static IReadOnlyDictionary<string, CompositeRepositorySpec> BuildCompositeRepositorySpecs()
        => new Dictionary<string, CompositeRepositorySpec>
        {
            ["update-single-row"] = new(
                Seed: () =>
                [
                    CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 2, comment: "repo-composite-a"),
                    CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 3, comment: "repo-composite-b")
                ],
                FilterFactory: entities => x => x.ProductId == entities[0].ProductId && x.CustomerId == entities[0].CustomerId,
                Setters: setters => setters
                    .SetProperty(x => x.Comment, x => "repo-composite-updated")
                    .SetProperty(x => x.Rating, x => x.Rating + 2),
                AssertPersisted: (seed, persisted, affected) =>
                {
                    affected.ShouldBe(1);
                    persisted.Count.ShouldBe(2);

                    var first = persisted.Single(x => x.ProductId == seed[0].ProductId && x.CustomerId == seed[0].CustomerId);
                    var second = persisted.Single(x => x.ProductId == seed[1].ProductId && x.CustomerId == seed[1].CustomerId);
                    first.Comment.ShouldBe("repo-composite-updated");
                    first.Rating.ShouldBe(seed[0].Rating + 2);
                    second.Comment.ShouldBe(seed[1].Comment);
                    second.Rating.ShouldBe(seed[1].Rating);
                }),

            ["update-multiple-nullables"] = new(
                Seed: () =>
                {
                    var prefix = $"EXU-CM-MULTI-{Guid.NewGuid():N}";
                    return
                    [
                        CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 3, comment: $"{prefix}-1", addedAt: new TimeOnly(11, 0)),
                        CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 4, comment: $"{prefix}-2", addedAt: new TimeOnly(12, 0))
                    ];
                },
                FilterFactory: entities =>
                {
                    var prefix = entities[0].Comment![..^2];
                    return x => x.Comment != null && x.Comment.StartsWith(prefix);
                },
                Setters: setters => setters
                    .SetProperty(x => x.Comment, x => null)
                    .SetProperty(x => x.AddedAt, x => (TimeOnly?)null),
                AssertPersisted: (_, persisted, affected) =>
                {
                    affected.ShouldBe(2);
                    persisted.Count.ShouldBe(2);
                    persisted.All(x => x.Comment is null).ShouldBeTrue();
                    persisted.All(x => x.AddedAt is null).ShouldBeTrue();
                }),

            ["no-match"] = new(
                Seed: () =>
                [
                    CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "repo-composite-no-match")
                ],
                FilterFactory: _ =>
                {
                    var missingProductId = Guid.NewGuid();
                    var missingCustomerId = Guid.NewGuid();
                    return x => x.ProductId == missingProductId && x.CustomerId == missingCustomerId;
                },
                Setters: setters => setters.SetProperty(x => x.Comment, x => "repo-composite-no-match-updated"),
                AssertPersisted: (seed, persisted, affected) =>
                {
                    affected.ShouldBe(0);
                    persisted.Count.ShouldBe(1);
                    persisted[0].Comment.ShouldBe(seed[0].Comment);
                    persisted[0].Rating.ShouldBe(seed[0].Rating);
                })
        };

    private static IReadOnlyDictionary<string, SingleApiSpec> BuildSingleApiSpecs()
        => new Dictionary<string, SingleApiSpec>
        {
            ["single-update-basic"] = new(
                Seed: () => CreateValidProduct(name: "api-single-before", sku: $"EXU-API-S-{Guid.NewGuid():N}", stockQuantity: 7, weight: 1.25m),
                BuildRequest: entity => new QueryRequest(Filters:
                [
                    new FilterClause("Id", "eq", entity.Id.ToString())
                ]),
                Updates:
                [
                    new("Weight", null)
                ],
                ExpectedAffected: 1,
                AssertPersisted: (_, persisted) =>
                {
                    persisted.Weight.ShouldBeNull();
                }),

            ["single-no-match"] = new(
                Seed: () => CreateValidProduct(name: "api-single-no-match-before", sku: $"EXU-API-S-NM-{Guid.NewGuid():N}", price: 17m, weight: 2.4m),
                BuildRequest: _ => new QueryRequest(Filters:
                [
                    new FilterClause("Id", "eq", Guid.NewGuid().ToString())
                ]),
                Updates:
                [
                    new("Weight", null)
                ],
                ExpectedAffected: 0,
                AssertPersisted: (seed, persisted) =>
                {
                    persisted.Weight.ShouldBe(seed.Weight);
                }),

            ["single-use-split-true"] = new(
                Seed: () => CreateValidProduct(name: "api-single-split-before", sku: $"EXU-API-S-SPLIT-{Guid.NewGuid():N}", count: 3),
                BuildRequest: entity => new QueryRequest(
                    Filters:
                    [
                        new FilterClause("Id", "eq", entity.Id.ToString())
                    ],
                    UseSplitQuery: true),
                Updates:
                [
                    new("Count", null)
                ],
                ExpectedAffected: 1,
                AssertPersisted: (_, persisted) =>
                {
                    persisted.Count.ShouldBeNull();
                })
        };

    private static IReadOnlyDictionary<string, CompositeApiSpec> BuildCompositeApiSpecs()
        => new Dictionary<string, CompositeApiSpec>
        {
            ["composite-update-basic"] = new(
                Seed: () => CreateValidReview(
                    productId: DataSeeder.productLaptopId,
                    customerId: DataSeeder.customerJohnId,
                    rating: 2,
                    comment: "api-composite-before"),
                BuildRequest: entity => new QueryRequest(Filters:
                [
                    new FilterClause("ProductId", "eq", entity.ProductId.ToString()),
                    new FilterClause("CustomerId", "eq", entity.CustomerId.ToString())
                ]),
                Updates:
                [
                    new("Comment", null)
                ],
                ExpectedAffected: 1,
                AssertPersisted: (_, persisted) =>
                {
                    persisted.Comment.ShouldBeNull();
                }),

            ["composite-no-match"] = new(
                Seed: () => CreateValidReview(
                    productId: DataSeeder.productHeadphonesId,
                    customerId: DataSeeder.customerJaneId,
                    rating: 4,
                    comment: "api-composite-no-match-before"),
                BuildRequest: _ => new QueryRequest(Filters:
                [
                    new FilterClause("ProductId", "eq", Guid.NewGuid().ToString()),
                    new FilterClause("CustomerId", "eq", Guid.NewGuid().ToString())
                ]),
                Updates:
                [
                    new("Comment", null)
                ],
                ExpectedAffected: 0,
                AssertPersisted: (seed, persisted) =>
                {
                    persisted.Comment.ShouldBe(seed.Comment);
                    persisted.Rating.ShouldBe(seed.Rating);
                }),

            ["composite-use-split-true"] = new(
                Seed: () => CreateValidReview(
                    productId: DataSeeder.productBookId,
                    customerId: DataSeeder.customerJohnId,
                    rating: 5,
                    comment: "api-composite-split-before"),
                BuildRequest: entity => new QueryRequest(
                    Filters:
                    [
                        new FilterClause("ProductId", "eq", entity.ProductId.ToString()),
                        new FilterClause("CustomerId", "eq", entity.CustomerId.ToString())
                    ],
                    UseSplitQuery: true),
                Updates:
                [
                    new("AddedAt", null)
                ],
                ExpectedAffected: 1,
                AssertPersisted: (_, persisted) =>
                {
                    persisted.AddedAt.ShouldBeNull();
                })
        };

    private static IReadOnlyDictionary<string, GlobalFilterSpec> BuildGlobalFilterSpecs()
        => new Dictionary<string, GlobalFilterSpec>
        {
            ["single-blocked"] = new(
                IsComposite: false,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(x => x.Price > 5000m),
                ExpectedAffected: 0),
            ["single-allowed"] = new(
                IsComposite: false,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(x => x.Price > 1000m),
                ExpectedAffected: 1),
            ["composite-blocked"] = new(
                IsComposite: true,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Review>(x => x.Rating < 5),
                ExpectedAffected: 0),
            ["composite-allowed"] = new(
                IsComposite: true,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Review>(x => x.Rating <= 5),
                ExpectedAffected: 1)
        };

    private sealed class RecordingProductBulkExecutor : IKyrolusBulkExecutor<Product>
    {
        public int ExecuteUpdateCalls { get; private set; }
        public bool? LastUseSplitQuery { get; private set; }
        public Expression<Func<Product, bool>>? LastFilter { get; private set; }
        public int ReturnValue { get; set; } = 7;

        public Task<int> ExecuteUpdateAsync(
            Expression<Func<Product, bool>>? filter,
            Action<UpdateSettersBuilder<Product>> setPropertyCalls,
            bool useSplitQuery,
            CancellationToken cancellationToken)
        {
            ExecuteUpdateCalls++;
            LastFilter = filter;
            LastUseSplitQuery = useSplitQuery;
            setPropertyCalls.ShouldNotBeNull();
            return Task.FromResult(ReturnValue);
        }

        public Task<int> ExecuteDeleteAsync(Expression<Func<Product, bool>>? filter, bool useSplitQuery, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<int> BulkInsertAsync(IEnumerable<Product> entities, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<int> BulkUpsertAsync(IEnumerable<Product> entities, Expression<Func<Product, bool>> matchOn, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
