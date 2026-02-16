using System.Linq.Expressions;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetPagedWithDefaultsAsyncTests;

public partial class GetPagedWithDefaultsAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    public static TheoryData<string, int, int, int, string[], int> SingleDefaultPagingCases => new()
    {
        { "single-default-page-and-size", 0, 0, 2, ["BOOK-CC", "NC-100"], 3 },
        { "single-default-size-only", 2, 0, 2, ["LP-15"], 3 },
        { "single-default-page-only", -1, 1, 2, ["BOOK-CC"], 3 }
    };

    public static TheoryData<string, int, int, int, int[], int> CompositeDefaultPagingCases => new()
    {
        { "composite-default-page-and-size", 0, 0, 2, [5, 4], 3 },
        { "composite-default-size-only", 2, 0, 2, [3], 3 },
        { "composite-default-page-only", -1, 1, 2, [5], 3 }
    };

    public static TheoryData<string, bool> KeyTypeCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    public static TheoryData<string, bool, bool, bool> IncludeMergeCases => new()
    {
        { "spec-and-params-includes", true, true, true },
        { "spec-only-includes", true, false, false },
        { "params-only-includes", false, true, true }
    };

    public static TheoryData<string, bool, bool?, bool, bool, bool> TrackingCases => new()
    {
        { "single-override-true", false, true, false, false, false },
        { "single-override-false", false, false, true, true, true },
        { "single-policy-true", false, null, true, false, false },
        { "single-policy-false", false, null, false, true, true },
        { "composite-override-true", true, true, false, false, false },
        { "composite-override-false", true, false, true, true, true },
        { "composite-policy-true", true, null, true, false, false },
        { "composite-policy-false", true, null, false, true, true }
    };

    private sealed record GlobalFilterSpec(bool IsComposite, KyrolusRepositoryPolicy Policy, bool ExpectedFound);
    private static readonly IReadOnlyDictionary<string, GlobalFilterSpec> GlobalFilterSpecs = BuildGlobalFilterSpecs();
    public static TheoryData<string> GlobalFilterCases => CaseIdsFrom(GlobalFilterSpecs);

    [Theory(DisplayName = "GetPagedWithDefaultsAsync applies page defaults for single-key entities")]
    [MemberData(nameof(SingleDefaultPagingCases))]
    public async Task GetPagedWithDefaultsAsync_SingleKey_DefaultPaging_Works(
        string caseId,
        int pageNumber,
        int pageSize,
        int policyPageSize,
        string[] expectedSkus,
        int expectedTotal)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { DefaultPageSize = policyPageSize });
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var spec = new TestPagedSpecification<Product, ProductPageProjection>
        {
            Filter = x => x.Price >= 35m,
            Selector = x => new ProductPageProjection(x.Id, x.Sku, x.Price),
            OrderBy = q => q.OrderBy(x => x.Price),
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var (items, totalCount) = await repo.GetPagedWithDefaultsAsync(spec);
        totalCount.ShouldBe(expectedTotal);
        items.Select(x => x.Sku).ShouldBe(expectedSkus);
    }

    [Theory(DisplayName = "GetPagedWithDefaultsAsync applies page defaults for composite-key entities")]
    [MemberData(nameof(CompositeDefaultPagingCases))]
    public async Task GetPagedWithDefaultsAsync_CompositeKey_DefaultPaging_Works(
        string caseId,
        int pageNumber,
        int pageSize,
        int policyPageSize,
        int[] expectedRatings,
        int expectedTotal)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { DefaultPageSize = policyPageSize });
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        var spec = new TestPagedSpecification<Review, ReviewPageProjection>
        {
            Filter = x => x.Rating >= 3,
            Selector = x => new ReviewPageProjection(x.ProductId, x.CustomerId, x.Rating, x.Comment),
            OrderBy = q => q.OrderByDescending(x => x.Rating),
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var (items, totalCount) = await repo.GetPagedWithDefaultsAsync(spec);
        totalCount.ShouldBe(expectedTotal);
        items.Select(x => x.Rating).ShouldBe(expectedRatings);
    }

    [Fact(DisplayName = "GetPagedWithDefaultsAsync merges specification and argument filters and order by for single-key entities")]
    public async Task GetPagedWithDefaultsAsync_SingleKey_MergeFilterAndOrderBy_Works()
    {
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { DefaultPageSize = 10 });
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var spec = new TestPagedSpecification<Product, ProductPageProjection>
        {
            Filter = x => x.Price >= 35m,
            Selector = x => new ProductPageProjection(x.Id, x.Sku, x.Price),
            OrderBy = q => q.OrderBy(x => x.Price),
            PageNumber = 0,
            PageSize = 0
        };

        var (items, totalCount) = await repo.GetPagedWithDefaultsAsync(
            specification: spec,
            filter: x => x.Sku != "NC-100",
            orderBy: q => q.OrderByDescending(x => x.Price),
            asNoTracking: true,
            useSplitQuery: false);

        totalCount.ShouldBe(2);
        items.Select(x => x.Sku).ShouldBe(["BOOK-CC", "LP-15"]);
    }

    [Fact(DisplayName = "GetPagedWithDefaultsAsync merges specification and argument filters and order by for composite-key entities")]
    public async Task GetPagedWithDefaultsAsync_CompositeKey_MergeFilterAndOrderBy_Works()
    {
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { DefaultPageSize = 10 });
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        var spec = new TestPagedSpecification<Review, ReviewPageProjection>
        {
            Filter = x => x.Rating >= 3,
            Selector = x => new ReviewPageProjection(x.ProductId, x.CustomerId, x.Rating, x.Comment),
            OrderBy = q => q.OrderByDescending(x => x.Rating),
            PageNumber = 0,
            PageSize = 0
        };

        var (items, totalCount) = await repo.GetPagedWithDefaultsAsync(
            specification: spec,
            filter: x => x.CustomerId == DataSeeder.customerJaneId,
            orderBy: q => q.OrderBy(x => x.Rating),
            asNoTracking: true,
            useSplitQuery: false);

        totalCount.ShouldBe(2);
        items.Select(x => x.Rating).ShouldBe([5, 4]);
    }

    [Theory(DisplayName = "GetPagedWithDefaultsAsync merges include expressions for single-key entities")]
    [MemberData(nameof(IncludeMergeCases))]
    public async Task GetPagedWithDefaultsAsync_SingleKey_IncludesMerge_Works(
        string caseId,
        bool hasSpecificationInclude,
        bool hasArgumentInclude,
        bool expectReviewsLoaded)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var spec = new TestPagedSpecification<Product, ProductPageProjection>
        {
            Filter = x => x.Id == DataSeeder.productLaptopId,
            Selector = x => new ProductPageProjection(x.Id, x.Sku, x.Price),
            Includes = hasSpecificationInclude ? [x => x.Store] : null,
            PageNumber = 1,
            PageSize = 1
        };

        Expression<Func<Product, object?>>[] includeExpressions = hasArgumentInclude ? [x => x.Reviews] : [];
        var (items, totalCount) = await repo.GetPagedWithDefaultsAsync(
            specification: spec,
            asNoTracking: false,
            useSplitQuery: false,
            includeExpressions: includeExpressions);

        totalCount.ShouldBe(1);
        items.Count.ShouldBe(1);
        var entry = db.Entry(items[0]);
        entry.Reference(x => x.Store).IsLoaded.ShouldBe(hasSpecificationInclude);
        entry.Collection(x => x.Reviews).IsLoaded.ShouldBe(expectReviewsLoaded);
    }

    [Fact(DisplayName = "GetPagedWithDefaultsAsync merges include expressions for composite-key entities")]
    public async Task GetPagedWithDefaultsAsync_CompositeKey_IncludesMerge_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        var spec = new TestPagedSpecification<Review, ReviewPageProjection>
        {
            Filter = x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId,
            Selector = x => new ReviewPageProjection(x.ProductId, x.CustomerId, x.Rating, x.Comment),
            Includes = [x => x.Product],
            PageNumber = 1,
            PageSize = 1
        };

        var (items, totalCount) = await repo.GetPagedWithDefaultsAsync(
            specification: spec,
            asNoTracking: false,
            useSplitQuery: false,
            includeExpressions: [x => x.Customer]);

        totalCount.ShouldBe(1);
        items.Count.ShouldBe(1);
        var entry = db.Entry(items[0]);
        entry.Reference(x => x.Product).IsLoaded.ShouldBeTrue();
        entry.Reference(x => x.Customer).IsLoaded.ShouldBeTrue();
    }

    [Theory(DisplayName = "GetPagedWithDefaultsAsync respects AsNoTracking argument then policy default")]
    [MemberData(nameof(TrackingCases))]
    public async Task GetPagedWithDefaultsAsync_AsNoTracking_Works(
        string caseId,
        bool compositeKey,
        bool? asNoTracking,
        bool policyAsNoTracking,
        bool specificationAsNoTracking,
        bool expectTracked)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = policyAsNoTracking });
        using var scope = customFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ChangeTracker.Clear();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var spec = new TestPagedSpecification<Review, ReviewPageProjection>
            {
                Filter = x => x.ProductId == DataSeeder.productLaptopId,
                Selector = x => new ReviewPageProjection(x.ProductId, x.CustomerId, x.Rating, x.Comment),
                AsNoTracking = specificationAsNoTracking,
                PageNumber = 1,
                PageSize = 5
            };
            _ = await repo.GetPagedWithDefaultsAsync(spec, asNoTracking: asNoTracking, useSplitQuery: false);
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var spec = new TestPagedSpecification<Product, ProductPageProjection>
            {
                Filter = x => x.Id == DataSeeder.productLaptopId,
                Selector = x => new ProductPageProjection(x.Id, x.Sku, x.Price),
                AsNoTracking = specificationAsNoTracking,
                PageNumber = 1,
                PageSize = 5
            };
            _ = await repo.GetPagedWithDefaultsAsync(spec, asNoTracking: asNoTracking, useSplitQuery: false);
        }

        if (expectTracked)
            db.ChangeTracker.Entries().ShouldNotBeEmpty();
        else
            db.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact(DisplayName = "GetPagedWithDefaultsAsync UseSplitQuery argument increases SQL commands with collection includes")]
    public async Task GetPagedWithDefaultsAsync_UseSplitQuery_Override_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        var spec = new TestPagedSpecification<Product, ProductPageProjection>
        {
            Filter = x => x.Id == DataSeeder.productLaptopId,
            Selector = x => new ProductPageProjection(x.Id, x.Sku, x.Price),
            PageNumber = 1,
            PageSize = 1
        };

        counter.Reset();
        var (nonSplitItems, nonSplitTotal) = await repo.GetPagedWithDefaultsAsync(
            specification: spec,
            asNoTracking: true,
            useSplitQuery: false,
            includeExpressions:
            [
                x => x.Reviews,
                x => x.OrderLines,
                x => x.ProductCategories
            ]);
        var nonSplitCommands = counter.Count;

        counter.Reset();
        var (splitItems, splitTotal) = await repo.GetPagedWithDefaultsAsync(
            specification: spec,
            asNoTracking: true,
            useSplitQuery: true,
            includeExpressions:
            [
                x => x.Reviews,
                x => x.OrderLines,
                x => x.ProductCategories
            ]);
        var splitCommands = counter.Count;

        nonSplitTotal.ShouldBe(1);
        splitTotal.ShouldBe(1);
        nonSplitItems.Count.ShouldBe(1);
        splitItems.Count.ShouldBe(1);
        nonSplitCommands.ShouldBeGreaterThan(0);
        splitCommands.ShouldBeGreaterThan(nonSplitCommands);
    }

    [Fact(DisplayName = "GetPagedWithDefaultsAsync uses policy split default when argument is null")]
    public async Task GetPagedWithDefaultsAsync_UseSplitQuery_UsesPolicyDefault()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            UseSplitQueryDefault = true,
            DefaultPageSize = 1
        };
        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        var spec = new TestPagedSpecification<Product, ProductPageProjection>
        {
            Filter = x => x.Id == DataSeeder.productLaptopId,
            Selector = x => new ProductPageProjection(x.Id, x.Sku, x.Price),
            PageNumber = 1,
            PageSize = 0
        };

        counter.Reset();
        var (items, totalCount) = await repo.GetPagedWithDefaultsAsync(
            specification: spec,
            asNoTracking: true,
            useSplitQuery: null,
            includeExpressions:
            [
                x => x.Reviews,
                x => x.OrderLines,
                x => x.ProductCategories
            ]);

        totalCount.ShouldBe(1);
        items.Count.ShouldBe(1);
        counter.Count.ShouldBeGreaterThan(2);
    }

    [Fact(DisplayName = "GetPagedWithDefaultsAsync ignores IKyrolusHasSplitQuery on specification")]
    public async Task GetPagedWithDefaultsAsync_UseSplitQuery_IgnoresSpecificationFlag()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        var spec = new TestPagedSpecificationWithSplit<Product, ProductPageProjection>
        {
            Filter = x => x.Id == DataSeeder.productLaptopId,
            Selector = x => new ProductPageProjection(x.Id, x.Sku, x.Price),
            UseSplitQuery = true,
            PageNumber = 1,
            PageSize = 1
        };

        counter.Reset();
        var (items, totalCount) = await repo.GetPagedWithDefaultsAsync(
            specification: spec,
            asNoTracking: true,
            useSplitQuery: null,
            includeExpressions:
            [
                x => x.Reviews,
                x => x.OrderLines,
                x => x.ProductCategories
            ]);

        totalCount.ShouldBe(1);
        items.Count.ShouldBe(1);
        counter.Count.ShouldBe(2);
    }

    [Theory(DisplayName = "GetPagedWithDefaultsAsync IncludeDeleted controls visibility of soft-deleted entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetPagedWithDefaultsAsync_IncludeDeleted_Works(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (compositeKey)
        {
            var review = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "paged-default-soft-composite");
            await SeedReviewAsync(review);
            try
            {
                await SoftDeleteReviewAsync(review.ProductId, review.CustomerId);
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

                var excludedSpec = new TestPagedSpecification<Review, ReviewPageProjection>
                {
                    Filter = x => x.ProductId == review.ProductId && x.CustomerId == review.CustomerId,
                    Selector = x => new ReviewPageProjection(x.ProductId, x.CustomerId, x.Rating, x.Comment),
                    IncludeDeleted = false,
                    PageNumber = 1,
                    PageSize = 5
                };
                var includedSpec = new TestPagedSpecification<Review, ReviewPageProjection>
                {
                    Filter = x => x.ProductId == review.ProductId && x.CustomerId == review.CustomerId,
                    Selector = x => new ReviewPageProjection(x.ProductId, x.CustomerId, x.Rating, x.Comment),
                    IncludeDeleted = true,
                    PageNumber = 1,
                    PageSize = 5
                };

                var (excludedItems, excludedTotal) = await repo.GetPagedWithDefaultsAsync(excludedSpec, asNoTracking: true, useSplitQuery: false);
                excludedItems.ShouldBeEmpty();
                excludedTotal.ShouldBe(0);

                var (includedItems, includedTotal) = await repo.GetPagedWithDefaultsAsync(includedSpec, asNoTracking: true, useSplitQuery: false);
                includedItems.Count.ShouldBe(1);
                includedTotal.ShouldBe(1);
            }
            finally
            {
                await CleanupReviewAsync(review.ProductId, review.CustomerId);
            }
            return;
        }

        var product = CreateValidProduct(name: "paged-default-soft-single");
        await SeedProductAsync(product);
        try
        {
            await SoftDeleteProductAsync(product.Id);
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

            var excludedSpec = new TestPagedSpecification<Product, ProductPageProjection>
            {
                Filter = x => x.Id == product.Id,
                Selector = x => new ProductPageProjection(x.Id, x.Sku, x.Price),
                IncludeDeleted = false,
                PageNumber = 1,
                PageSize = 5
            };
            var includedSpec = new TestPagedSpecification<Product, ProductPageProjection>
            {
                Filter = x => x.Id == product.Id,
                Selector = x => new ProductPageProjection(x.Id, x.Sku, x.Price),
                IncludeDeleted = true,
                PageNumber = 1,
                PageSize = 5
            };

            var (excludedItems, excludedTotal) = await repo.GetPagedWithDefaultsAsync(excludedSpec, asNoTracking: true, useSplitQuery: false);
            excludedItems.ShouldBeEmpty();
            excludedTotal.ShouldBe(0);

            var (includedItems, includedTotal) = await repo.GetPagedWithDefaultsAsync(includedSpec, asNoTracking: true, useSplitQuery: false);
            includedItems.Count.ShouldBe(1);
            includedTotal.ShouldBe(1);
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "GetPagedWithDefaultsAsync respects global filters")]
    [MemberData(nameof(GlobalFilterCases))]
    public async Task GetPagedWithDefaultsAsync_GlobalFilters_Work(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = GlobalFilterSpecs[caseId];
        var customFactory = WithPolicy(spec.Policy);
        using var scope = customFactory.Services.CreateScope();

        if (spec.IsComposite)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var pagedSpec = new TestPagedSpecification<Review, ReviewPageProjection>
            {
                Filter = x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId,
                Selector = x => new ReviewPageProjection(x.ProductId, x.CustomerId, x.Rating, x.Comment),
                PageNumber = 1,
                PageSize = 5
            };
            var (items, total) = await repo.GetPagedWithDefaultsAsync(pagedSpec, asNoTracking: true, useSplitQuery: false);
            if (spec.ExpectedFound)
            {
                items.Count.ShouldBe(1);
                total.ShouldBe(1);
            }
            else
            {
                items.ShouldBeEmpty();
                total.ShouldBe(0);
            }
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleSpec = new TestPagedSpecification<Product, ProductPageProjection>
        {
            Filter = x => x.Id == DataSeeder.productLaptopId,
            Selector = x => new ProductPageProjection(x.Id, x.Sku, x.Price),
            PageNumber = 1,
            PageSize = 5
        };
        var (singleItems, singleTotal) = await singleRepo.GetPagedWithDefaultsAsync(singleSpec, asNoTracking: true, useSplitQuery: false);
        if (spec.ExpectedFound)
        {
            singleItems.Count.ShouldBe(1);
            singleTotal.ShouldBe(1);
        }
        else
        {
            singleItems.ShouldBeEmpty();
            singleTotal.ShouldBe(0);
        }
    }

    [Theory(DisplayName = "GetPagedWithDefaultsAsync does not require specification selector")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetPagedWithDefaultsAsync_NullSelector_IsIgnored(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var spec = new TestPagedSpecification<Review, ReviewPageProjection>
            {
                Filter = x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId,
                Selector = null,
                PageNumber = 1,
                PageSize = 1
            };
            var (items, total) = await repo.GetPagedWithDefaultsAsync(spec, asNoTracking: true, useSplitQuery: false);
            total.ShouldBe(1);
            items.Count.ShouldBe(1);
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleSpec = new TestPagedSpecification<Product, ProductPageProjection>
        {
            Filter = x => x.Id == DataSeeder.productLaptopId,
            Selector = null,
            PageNumber = 1,
            PageSize = 1
        };
        var (singleItems, singleTotal) = await singleRepo.GetPagedWithDefaultsAsync(singleSpec, asNoTracking: true, useSplitQuery: false);
        singleTotal.ShouldBe(1);
        singleItems.Count.ShouldBe(1);
    }

    private static IReadOnlyDictionary<string, GlobalFilterSpec> BuildGlobalFilterSpecs()
        => new Dictionary<string, GlobalFilterSpec>
        {
            ["single-blocked"] = new(
                IsComposite: false,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(x => x.Price > 5000m),
                ExpectedFound: false),
            ["single-allowed"] = new(
                IsComposite: false,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(x => x.Price >= 1000m),
                ExpectedFound: true),
            ["composite-blocked"] = new(
                IsComposite: true,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Review>(x => x.Rating < 5),
                ExpectedFound: false),
            ["composite-allowed"] = new(
                IsComposite: true,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Review>(x => x.Rating <= 5),
                ExpectedFound: true)
        };
}
