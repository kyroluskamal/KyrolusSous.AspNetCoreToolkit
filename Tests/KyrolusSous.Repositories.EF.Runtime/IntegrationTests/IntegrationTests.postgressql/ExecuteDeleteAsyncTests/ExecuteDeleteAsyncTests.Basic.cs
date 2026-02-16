using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.ExecuteDeleteAsyncTests;

public partial class ExecuteDeleteAsyncTests
{
    private sealed record SingleRepositorySpec(
        Func<List<Product>> Seed,
        Func<List<Product>, Expression<Func<Product, bool>>?> FilterFactory,
        Action<List<Product>, List<Product>, int> AssertPersisted);

    private sealed record CompositeRepositorySpec(
        Func<List<Review>> Seed,
        Func<List<Review>, Expression<Func<Review, bool>>?> FilterFactory,
        Action<List<Review>, List<Review>, int> AssertPersisted);

    private sealed record SingleApiSpec(
        Func<Product> Seed,
        Func<Product, QueryRequest> BuildRequest,
        int ExpectedAffected,
        Action<Product, Product?> AssertPersisted);

    private sealed record CompositeApiSpec(
        Func<Review> Seed,
        Func<Review, QueryRequest> BuildRequest,
        int ExpectedAffected,
        Action<Review, Review?> AssertPersisted);

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

    [Theory(DisplayName = "ExecuteDeleteAsync deletes single-key entities via repository")]
    [MemberData(nameof(SingleRepositoryCases))]
    public async Task ExecuteDeleteAsync_SingleKey_Repository_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SingleRepositorySpecs[caseId];
        var seed = spec.Seed();
        await SeedProductsAsync(seed);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

            var affected = await repo.ExecuteDeleteAsync(spec.FilterFactory(seed), useSplitQuery: false);
            var ids = seed.Select(x => x.Id).ToArray();
            var persisted = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Products.IgnoreQueryFilters().AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .ToListAsync();

            spec.AssertPersisted(seed, persisted, affected);
        }
        finally
        {
            await CleanupProductsAsync(seed.Select(x => x.Id));
        }
    }

    [Theory(DisplayName = "ExecuteDeleteAsync deletes composite-key entities via repository")]
    [MemberData(nameof(CompositeRepositoryCases))]
    public async Task ExecuteDeleteAsync_CompositeKey_Repository_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = CompositeRepositorySpecs[caseId];
        var seed = spec.Seed();
        await SeedReviewsAsync(seed);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

            var affected = await repo.ExecuteDeleteAsync(spec.FilterFactory(seed), useSplitQuery: false);
            var keys = seed.Select(x => (x.ProductId, x.CustomerId)).ToArray();
            var persisted = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Reviews.IgnoreQueryFilters().AsNoTracking()
                .Where(x => keys.Select(k => k.ProductId).Contains(x.ProductId) && keys.Select(k => k.CustomerId).Contains(x.CustomerId))
                .ToListAsync();

            persisted = persisted
                .Where(x => keys.Any(k => k.ProductId == x.ProductId && k.CustomerId == x.CustomerId))
                .ToList();

            spec.AssertPersisted(seed, persisted, affected);
        }
        finally
        {
            await CleanupReviewsAsync(seed.Select(x => (x.ProductId, x.CustomerId)));
        }
    }

    [Theory(DisplayName = "ExecuteDeleteAsync respects split query option for single-key entities")]
    [MemberData(nameof(UseSplitCases))]
    public async Task ExecuteDeleteAsync_SingleKey_SplitQueryOption_Works(string caseId, bool? useSplitQuery, bool policyDefault)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var policy = new KyrolusRepositoryPolicy { UseSplitQueryDefault = policyDefault };
        var customFactory = WithPolicy(policy);

        var entity = CreateValidProduct(name: $"split-single-{caseId}", sku: $"EXD-SPLIT-S-{Guid.NewGuid():N}");
        await using var prepScope = customFactory.Services.CreateAsyncScope();
        prepScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Products.Add(entity);
        await prepScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SaveChangesAsync();

        try
        {
            using var scope = customFactory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var affected = await repo.ExecuteDeleteAsync(x => x.Id == entity.Id, useSplitQuery);
            affected.ShouldBe(1);

            var persisted = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Products.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == entity.Id);
            persisted.ShouldBeNull();
        }
        finally
        {
            await CleanupProductAsync(entity.Id);
        }
    }

    [Theory(DisplayName = "ExecuteDeleteAsync respects split query option for composite-key entities")]
    [MemberData(nameof(UseSplitCases))]
    public async Task ExecuteDeleteAsync_CompositeKey_SplitQueryOption_Works(string caseId, bool? useSplitQuery, bool policyDefault)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var policy = new KyrolusRepositoryPolicy { UseSplitQueryDefault = policyDefault };
        var customFactory = WithPolicy(policy);

        var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 3, comment: $"split-composite-{caseId}");
        await using var prepScope = customFactory.Services.CreateAsyncScope();
        prepScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Reviews.Add(entity);
        await prepScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SaveChangesAsync();

        try
        {
            using var scope = customFactory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var affected = await repo.ExecuteDeleteAsync(
                x => x.ProductId == entity.ProductId && x.CustomerId == entity.CustomerId,
                useSplitQuery);
            affected.ShouldBe(1);

            var persisted = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Reviews.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(x => x.ProductId == entity.ProductId && x.CustomerId == entity.CustomerId);
            persisted.ShouldBeNull();
        }
        finally
        {
            await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
        }
    }

    [Fact(DisplayName = "ExecuteDeleteAsync excludes soft-deleted single-key entities")]
    public async Task ExecuteDeleteAsync_SingleKey_SoftDeletedEntity_IsExcluded()
    {
        var entity = CreateValidProduct(name: "exec-delete-soft-single");
        await SeedProductAsync(entity);

        try
        {
            await SoftDeleteProductAsync(entity.Id);
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

            var affected = await repo.ExecuteDeleteAsync(x => x.Id == entity.Id, useSplitQuery: false);
            affected.ShouldBe(0);

            var persisted = await FindProductAsync(entity.Id, ignoreFilters: true);
            persisted.ShouldNotBeNull();
            persisted!.IsDeleted.ShouldBeTrue();
        }
        finally
        {
            await CleanupProductAsync(entity.Id);
        }
    }

    [Fact(DisplayName = "ExecuteDeleteAsync excludes soft-deleted composite-key entities")]
    public async Task ExecuteDeleteAsync_CompositeKey_SoftDeletedEntity_IsExcluded()
    {
        var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 4, comment: "exec-delete-soft-composite");
        await SeedReviewAsync(entity);

        try
        {
            await SoftDeleteReviewAsync(entity.ProductId, entity.CustomerId);
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

            var affected = await repo.ExecuteDeleteAsync(
                x => x.ProductId == entity.ProductId && x.CustomerId == entity.CustomerId,
                useSplitQuery: false);
            affected.ShouldBe(0);

            var persisted = await FindReviewAsync(entity.ProductId, entity.CustomerId, ignoreFilters: true);
            persisted.ShouldNotBeNull();
            persisted!.IsDeleted.ShouldBeTrue();
        }
        finally
        {
            await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
        }
    }

    [Theory(DisplayName = "ExecuteDeleteAsync respects global filters")]
    [MemberData(nameof(GlobalFilterCases))]
    public async Task ExecuteDeleteAsync_GlobalFilters_Work(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = GlobalFilterSpecs[caseId];
        var customFactory = WithPolicy(spec.Policy);

        if (spec.IsComposite)
        {
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 5, comment: $"global-{caseId}");
            await using var prepScope = customFactory.Services.CreateAsyncScope();
            prepScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Reviews.Add(entity);
            await prepScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SaveChangesAsync();

            try
            {
                using var scope = customFactory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var affected = await repo.ExecuteDeleteAsync(
                    x => x.ProductId == entity.ProductId && x.CustomerId == entity.CustomerId,
                    useSplitQuery: false);
                affected.ShouldBe(spec.ExpectedAffected);

                var persisted = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                    .Reviews.IgnoreQueryFilters().AsNoTracking()
                    .SingleOrDefaultAsync(x => x.ProductId == entity.ProductId && x.CustomerId == entity.CustomerId);
                if (spec.ExpectedAffected == 0)
                    persisted.ShouldNotBeNull();
                else
                    persisted.ShouldBeNull();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singleEntity = CreateValidProduct(name: $"global-{caseId}", sku: $"EXD-G-{Guid.NewGuid():N}");
        singleEntity.Price = 1200m;
        await using var prepSingleScope = customFactory.Services.CreateAsyncScope();
        prepSingleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Products.Add(singleEntity);
        await prepSingleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SaveChangesAsync();

        try
        {
            using var scope = customFactory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var affected = await repo.ExecuteDeleteAsync(x => x.Id == singleEntity.Id, useSplitQuery: false);
            affected.ShouldBe(spec.ExpectedAffected);

            var persisted = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Products.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == singleEntity.Id);
            if (spec.ExpectedAffected == 0)
                persisted.ShouldNotBeNull();
            else
                persisted.ShouldBeNull();
        }
        finally
        {
            await CleanupProductAsync(singleEntity.Id);
        }
    }

    [Fact(DisplayName = "ExecuteDeleteAsync invalidates GetAllAsync cache entries")]
    public async Task ExecuteDeleteAsync_Invalidates_GetAllAsync_Cache()
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

        var entity = CreateValidProduct(name: "cache-before", sku: $"EXD-CACHE-{Guid.NewGuid():N}");
        db.Products.Add(entity);
        await db.SaveChangesAsync();

        cache.Clear();
        cache.Count.ShouldBe(0);

        counter.Reset();
        _ = (await repo.GetAllAsync()).ToList();
        cache.Count.ShouldBe(1);
        counter.Count.ShouldBeGreaterThan(0);

        counter.Reset();
        _ = (await repo.GetAllAsync()).ToList();
        counter.Count.ShouldBe(0);

        var affected = await repo.ExecuteDeleteAsync(x => x.Id == entity.Id, useSplitQuery: false);
        affected.ShouldBe(1);

        counter.Reset();
        var afterDelete = (await repo.GetAllAsync()).ToList();
        counter.Count.ShouldBeGreaterThan(0);
        afterDelete.Any(x => x.Id == entity.Id).ShouldBeFalse();
    }

    [Fact(DisplayName = "ExecuteDeleteAsync uses bulk executor when available")]
    public async Task ExecuteDeleteAsync_UsesBulkExecutor_WhenRegistered()
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

        var affected = await repo.ExecuteDeleteAsync(x => x.Id == DataSeeder.productLaptopId, useSplitQuery: true);
        affected.ShouldBe(recorder.ReturnValue);
        recorder.ExecuteDeleteCalls.ShouldBe(1);
        recorder.LastUseSplitQuery.ShouldBe(true);
        recorder.LastFilter.ShouldNotBeNull();

        var persisted = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == DataSeeder.productLaptopId);
        persisted.ShouldNotBeNull();
    }

    [Theory(DisplayName = "ExecuteDelete API deletes single-key entities")]
    [MemberData(nameof(SingleApiCases))]
    public async Task ExecuteDeleteAsync_Api_SingleKey_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SingleApiSpecs[caseId];
        var entity = spec.Seed();
        await SeedProductAsync(entity);

        try
        {
            var (response, affected, content) = await PostExecuteDeleteAsync<Product>(spec.BuildRequest(entity));
            response.StatusCode.ShouldBe(HttpStatusCode.OK, content);
            affected.ShouldNotBeNull();
            affected!.Value.ShouldBe(spec.ExpectedAffected);

            var persisted = await FindProductAsync(entity.Id, ignoreFilters: true);
            spec.AssertPersisted(entity, persisted);
        }
        finally
        {
            await CleanupProductAsync(entity.Id);
        }
    }

    [Theory(DisplayName = "ExecuteDelete API deletes composite-key entities")]
    [MemberData(nameof(CompositeApiCases))]
    public async Task ExecuteDeleteAsync_Api_CompositeKey_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = CompositeApiSpecs[caseId];
        var entity = spec.Seed();
        await SeedReviewAsync(entity);

        try
        {
            var (response, affected, content) = await PostExecuteDeleteAsync<Review>(spec.BuildRequest(entity));
            response.StatusCode.ShouldBe(HttpStatusCode.OK, content);
            affected.ShouldNotBeNull();
            affected!.Value.ShouldBe(spec.ExpectedAffected);

            var persisted = await FindReviewAsync(entity.ProductId, entity.CustomerId, ignoreFilters: true);
            spec.AssertPersisted(entity, persisted);
        }
        finally
        {
            await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
        }
    }

    [Fact(DisplayName = "ExecuteDelete API excludes soft-deleted single-key entities")]
    public async Task ExecuteDeleteAsync_Api_SingleKey_SoftDeletedEntity_IsExcluded()
    {
        var entity = CreateValidProduct(name: "api-soft-single-delete");
        await SeedProductAsync(entity);

        try
        {
            await SoftDeleteProductAsync(entity.Id);
            var request = new QueryRequest(Filters:
            [
                new FilterClause("Id", "eq", entity.Id.ToString())
            ]);

            var (response, affected, content) = await PostExecuteDeleteAsync<Product>(request);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, content);
            affected.ShouldNotBeNull();
            affected!.Value.ShouldBe(0);

            var persisted = await FindProductAsync(entity.Id, ignoreFilters: true);
            persisted.ShouldNotBeNull();
            persisted!.IsDeleted.ShouldBeTrue();
        }
        finally
        {
            await CleanupProductAsync(entity.Id);
        }
    }

    [Fact(DisplayName = "ExecuteDelete API excludes soft-deleted composite-key entities")]
    public async Task ExecuteDeleteAsync_Api_CompositeKey_SoftDeletedEntity_IsExcluded()
    {
        var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 4, comment: "api-soft-composite-delete");
        await SeedReviewAsync(entity);

        try
        {
            await SoftDeleteReviewAsync(entity.ProductId, entity.CustomerId);
            var request = new QueryRequest(Filters:
            [
                new FilterClause("ProductId", "eq", entity.ProductId.ToString()),
                new FilterClause("CustomerId", "eq", entity.CustomerId.ToString())
            ]);

            var (response, affected, content) = await PostExecuteDeleteAsync<Review>(request);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, content);
            affected.ShouldNotBeNull();
            affected!.Value.ShouldBe(0);

            var persisted = await FindReviewAsync(entity.ProductId, entity.CustomerId, ignoreFilters: true);
            persisted.ShouldNotBeNull();
            persisted!.IsDeleted.ShouldBeTrue();
        }
        finally
        {
            await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
        }
    }
    private static IReadOnlyDictionary<string, SingleRepositorySpec> BuildSingleRepositorySpecs()
        => new Dictionary<string, SingleRepositorySpec>
        {
            ["delete-single-row"] = new(
                Seed: () =>
                [
                    CreateValidProduct(name: "repo-single-a", sku: $"EXD-SN-A-{Guid.NewGuid():N}"),
                    CreateValidProduct(name: "repo-single-b", sku: $"EXD-SN-B-{Guid.NewGuid():N}")
                ],
                FilterFactory: entities => x => x.Id == entities[0].Id,
                AssertPersisted: (seed, persisted, affected) =>
                {
                    affected.ShouldBe(1);
                    persisted.Count.ShouldBe(1);
                    persisted[0].Id.ShouldBe(seed[1].Id);
                }),

            ["delete-multiple-rows"] = new(
                Seed: () =>
                {
                    var prefix = $"EXD-SN-M-{Guid.NewGuid():N}";
                    return
                    [
                        CreateValidProduct(name: "repo-multi-a", sku: $"{prefix}-1"),
                        CreateValidProduct(name: "repo-multi-b", sku: $"{prefix}-2")
                    ];
                },
                FilterFactory: entities =>
                {
                    var prefix = entities[0].Sku[..^2];
                    return x => x.Sku.StartsWith(prefix);
                },
                AssertPersisted: (_, persisted, affected) =>
                {
                    affected.ShouldBe(2);
                    persisted.ShouldBeEmpty();
                }),

            ["no-match"] = new(
                Seed: () =>
                [
                    CreateValidProduct(name: "repo-no-match", sku: $"EXD-SN-NM-{Guid.NewGuid():N}")
                ],
                FilterFactory: _ =>
                {
                    var missingId = Guid.NewGuid();
                    return x => x.Id == missingId;
                },
                AssertPersisted: (seed, persisted, affected) =>
                {
                    affected.ShouldBe(0);
                    persisted.Count.ShouldBe(1);
                    persisted[0].Id.ShouldBe(seed[0].Id);
                })
        };

    private static IReadOnlyDictionary<string, CompositeRepositorySpec> BuildCompositeRepositorySpecs()
        => new Dictionary<string, CompositeRepositorySpec>
        {
            ["delete-single-row"] = new(
                Seed: () =>
                [
                    CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 3, comment: "repo-composite-a"),
                    CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 4, comment: "repo-composite-b")
                ],
                FilterFactory: entities => x => x.ProductId == entities[0].ProductId && x.CustomerId == entities[0].CustomerId,
                AssertPersisted: (seed, persisted, affected) =>
                {
                    affected.ShouldBe(1);
                    persisted.Count.ShouldBe(1);
                    persisted[0].ProductId.ShouldBe(seed[1].ProductId);
                    persisted[0].CustomerId.ShouldBe(seed[1].CustomerId);
                }),

            ["delete-multiple-rows"] = new(
                Seed: () =>
                {
                    var token = $"EXD-CM-M-{Guid.NewGuid():N}";
                    return
                    [
                        CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 2, comment: $"{token}-1"),
                        CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 2, comment: $"{token}-2")
                    ];
                },
                FilterFactory: _ => x => x.Rating == 2,
                AssertPersisted: (_, persisted, affected) =>
                {
                    affected.ShouldBeGreaterThanOrEqualTo(2);
                    persisted.ShouldBeEmpty();
                }),

            ["no-match"] = new(
                Seed: () =>
                [
                    CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 4, comment: "repo-composite-no-match")
                ],
                FilterFactory: _ =>
                {
                    var missingProductId = Guid.NewGuid();
                    return x => x.ProductId == missingProductId;
                },
                AssertPersisted: (seed, persisted, affected) =>
                {
                    affected.ShouldBe(0);
                    persisted.Count.ShouldBe(1);
                    persisted[0].ProductId.ShouldBe(seed[0].ProductId);
                    persisted[0].CustomerId.ShouldBe(seed[0].CustomerId);
                })
        };

    private static IReadOnlyDictionary<string, SingleApiSpec> BuildSingleApiSpecs()
        => new Dictionary<string, SingleApiSpec>
        {
            ["delete-single"] = new(
                Seed: () => CreateValidProduct(name: "api-single-delete", sku: $"EXD-API-S-{Guid.NewGuid():N}"),
                BuildRequest: entity => new QueryRequest(Filters:
                [
                    new FilterClause("Id", "eq", entity.Id.ToString())
                ]),
                ExpectedAffected: 1,
                AssertPersisted: (_, persisted) => persisted.ShouldBeNull()),

            ["no-match"] = new(
                Seed: () => CreateValidProduct(name: "api-single-no-match", sku: $"EXD-API-S-NM-{Guid.NewGuid():N}"),
                BuildRequest: _ => new QueryRequest(Filters:
                [
                    new FilterClause("Id", "eq", Guid.NewGuid().ToString())
                ]),
                ExpectedAffected: 0,
                AssertPersisted: (_, persisted) => persisted.ShouldNotBeNull()),

            ["use-split-true"] = new(
                Seed: () => CreateValidProduct(name: "api-single-split", sku: $"EXD-API-S-SPLIT-{Guid.NewGuid():N}"),
                BuildRequest: entity => new QueryRequest(
                    Filters:
                    [
                        new FilterClause("Id", "eq", entity.Id.ToString())
                    ],
                    UseSplitQuery: true),
                ExpectedAffected: 1,
                AssertPersisted: (_, persisted) => persisted.ShouldBeNull())
        };

    private static IReadOnlyDictionary<string, CompositeApiSpec> BuildCompositeApiSpecs()
        => new Dictionary<string, CompositeApiSpec>
        {
            ["delete-single"] = new(
                Seed: () => CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 4, comment: "api-composite-delete"),
                BuildRequest: entity => new QueryRequest(Filters:
                [
                    new FilterClause("ProductId", "eq", entity.ProductId.ToString()),
                    new FilterClause("CustomerId", "eq", entity.CustomerId.ToString())
                ]),
                ExpectedAffected: 1,
                AssertPersisted: (_, persisted) => persisted.ShouldBeNull()),

            ["no-match"] = new(
                Seed: () => CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 3, comment: "api-composite-no-match"),
                BuildRequest: _ => new QueryRequest(Filters:
                [
                    new FilterClause("ProductId", "eq", Guid.NewGuid().ToString()),
                    new FilterClause("CustomerId", "eq", Guid.NewGuid().ToString())
                ]),
                ExpectedAffected: 0,
                AssertPersisted: (_, persisted) => persisted.ShouldNotBeNull()),

            ["use-split-true"] = new(
                Seed: () => CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 2, comment: "api-composite-split"),
                BuildRequest: entity => new QueryRequest(
                    Filters:
                    [
                        new FilterClause("ProductId", "eq", entity.ProductId.ToString()),
                        new FilterClause("CustomerId", "eq", entity.CustomerId.ToString())
                    ],
                    UseSplitQuery: true),
                ExpectedAffected: 1,
                AssertPersisted: (_, persisted) => persisted.ShouldBeNull())
        };

    private static IReadOnlyDictionary<string, GlobalFilterSpec> BuildGlobalFilterSpecs()
        => new Dictionary<string, GlobalFilterSpec>
        {
            ["single-blocked"] = new(
                IsComposite: false,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(x => x.Price > 2000m),
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
        public int ExecuteDeleteCalls { get; private set; }
        public bool? LastUseSplitQuery { get; private set; }
        public Expression<Func<Product, bool>>? LastFilter { get; private set; }
        public int ReturnValue { get; set; } = 5;

        public Task<int> ExecuteDeleteAsync(Expression<Func<Product, bool>>? filter, bool useSplitQuery, CancellationToken cancellationToken)
        {
            ExecuteDeleteCalls++;
            LastFilter = filter;
            LastUseSplitQuery = useSplitQuery;
            return Task.FromResult(ReturnValue);
        }

        public Task<int> ExecuteUpdateAsync(Expression<Func<Product, bool>>? filter, Action<UpdateSettersBuilder<Product>> setPropertyCalls, bool useSplitQuery, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<int> BulkInsertAsync(IEnumerable<Product> entities, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<int> BulkUpsertAsync(IEnumerable<Product> entities, Expression<Func<Product, bool>> matchOn, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
