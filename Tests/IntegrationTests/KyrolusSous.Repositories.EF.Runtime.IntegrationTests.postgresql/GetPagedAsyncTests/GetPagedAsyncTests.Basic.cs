namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetPagedAsyncTests;

public partial class GetPagedAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    public static TheoryData<string, int, int, string[], int> SinglePagingCases => new()
    {
        { "single-page-1", 1, 2, ["BOOK-CC", "NC-100"], 3 },
        { "single-page-2", 2, 2, ["LP-15"], 3 },
        { "single-page-3-empty", 3, 2, [], 3 }
    };

    public static TheoryData<string, int, int, int[], int> CompositePagingCases => new()
    {
        { "composite-page-1", 1, 2, [5, 4], 3 },
        { "composite-page-2", 2, 2, [3], 3 }
    };

    public static TheoryData<string, bool, bool> TrackingCases => new()
    {
        { "single-asnotracking-true", false, true },
        { "single-asnotracking-false", false, false },
        { "composite-asnotracking-true", true, true },
        { "composite-asnotracking-false", true, false }
    };

    public static TheoryData<string, bool> KeyTypeCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    private sealed record GlobalFilterSpec(bool IsComposite, KyrolusRepositoryPolicy Policy, bool ExpectedFound);
    private static readonly IReadOnlyDictionary<string, GlobalFilterSpec> GlobalFilterSpecs = BuildGlobalFilterSpecs();
    public static TheoryData<string> GlobalFilterCases => CaseIdsFrom(GlobalFilterSpecs);

    [Theory(DisplayName = "GetPagedAsync returns expected pages for single-key entities")]
    [MemberData(nameof(SinglePagingCases))]
    public async Task GetPagedAsync_SingleKey_Paging_Works(string caseId, int pageNumber, int pageSize, string[] expectedSkus, int expectedTotal)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var spec = new TestPagedSpecification<Product, ProductPageProjection>
        {
            Filter = x => x.Price >= 35m,
            Selector = x => new ProductPageProjection(x.Id, x.Sku, x.Price),
            OrderBy = q => q.OrderBy(x => x.Price),
            AsNoTracking = true,
            UseSplitQuery = false,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var (items, totalCount) = await repo.GetPagedAsync(spec);
        totalCount.ShouldBe(expectedTotal);
        items.Select(x => x.Sku).ShouldBe(expectedSkus);
    }

    [Theory(DisplayName = "GetPagedAsync returns expected pages for composite-key entities")]
    [MemberData(nameof(CompositePagingCases))]
    public async Task GetPagedAsync_CompositeKey_Paging_Works(string caseId, int pageNumber, int pageSize, int[] expectedRatings, int expectedTotal)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        var spec = new TestPagedSpecification<Review, ReviewPageProjection>
        {
            Filter = x => x.Rating >= 3,
            Selector = x => new ReviewPageProjection(x.ProductId, x.CustomerId, x.Rating, x.Comment),
            OrderBy = q => q.OrderByDescending(x => x.Rating),
            AsNoTracking = true,
            UseSplitQuery = false,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var (items, totalCount) = await repo.GetPagedAsync(spec);
        totalCount.ShouldBe(expectedTotal);
        items.Select(x => x.Rating).ShouldBe(expectedRatings);
    }

    [Fact(DisplayName = "GetPagedAsync supports include expressions for single-key entities")]
    public async Task GetPagedAsync_SingleKey_Includes_Work()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var spec = new TestPagedSpecification<Product, Product>
        {
            Filter = x => x.Id == DataSeeder.productLaptopId,
            Selector = x => x,
            Includes = [x => x.Reviews, x => x.Store],
            AsNoTracking = false,
            UseSplitQuery = false,
            PageNumber = 1,
            PageSize = 1
        };

        var (items, total) = await repo.GetPagedAsync(spec);
        total.ShouldBe(1);
        items.Count.ShouldBe(1);
        items[0].Reviews.ShouldNotBeEmpty();
        items[0].Store.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetPagedAsync supports include expressions for composite-key entities")]
    public async Task GetPagedAsync_CompositeKey_Includes_Work()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        var spec = new TestPagedSpecification<Review, Review>
        {
            Filter = x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId,
            Selector = x => x,
            Includes = [x => x.Product, x => x.Customer],
            AsNoTracking = false,
            UseSplitQuery = false,
            PageNumber = 1,
            PageSize = 1
        };

        var (items, total) = await repo.GetPagedAsync(spec);
        total.ShouldBe(1);
        items.Count.ShouldBe(1);
        items[0].Product.ShouldNotBeNull();
        items[0].Customer.ShouldNotBeNull();
    }

    [Theory(DisplayName = "GetPagedAsync respects AsNoTracking settings")]
    [MemberData(nameof(TrackingCases))]
    public async Task GetPagedAsync_AsNoTracking_Works(string caseId, bool compositeKey, bool asNoTracking)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ChangeTracker.Clear();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var spec = new TestPagedSpecification<Review, Review>
            {
                Filter = x => x.ProductId == DataSeeder.productLaptopId,
                Selector = x => x,
                AsNoTracking = asNoTracking,
                UseSplitQuery = false,
                PageNumber = 1,
                PageSize = 5
            };
            _ = await repo.GetPagedAsync(spec);
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var spec = new TestPagedSpecification<Product, Product>
            {
                Filter = x => x.Id == DataSeeder.productLaptopId,
                Selector = x => x,
                AsNoTracking = asNoTracking,
                UseSplitQuery = false,
                PageNumber = 1,
                PageSize = 5
            };
            _ = await repo.GetPagedAsync(spec);
        }

        if (asNoTracking)
            db.ChangeTracker.Entries().ShouldBeEmpty();
        else
            db.ChangeTracker.Entries().ShouldNotBeEmpty();
    }

    [Fact(DisplayName = "GetPagedAsync UseSplitQuery increases SQL commands with collection includes")]
    public async Task GetPagedAsync_UseSplitQuery_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        var nonSplitSpec = new TestPagedSpecification<Product, Product>
        {
            Filter = x => x.Id == DataSeeder.productLaptopId,
            Selector = x => x,
            Includes = [x => x.Reviews, x => x.OrderLines, x => x.ProductCategories],
            AsNoTracking = true,
            UseSplitQuery = false,
            PageNumber = 1,
            PageSize = 1
        };
        counter.Reset();
        _ = await repo.GetPagedAsync(nonSplitSpec);
        var nonSplitCommands = counter.Count;

        var splitSpec = new TestPagedSpecification<Product, Product>
        {
            Filter = x => x.Id == DataSeeder.productLaptopId,
            Selector = x => x,
            Includes = [x => x.Reviews, x => x.OrderLines, x => x.ProductCategories],
            AsNoTracking = true,
            UseSplitQuery = true,
            PageNumber = 1,
            PageSize = 1
        };
        counter.Reset();
        _ = await repo.GetPagedAsync(splitSpec);
        var splitCommands = counter.Count;

        nonSplitCommands.ShouldBeGreaterThan(0);
        splitCommands.ShouldBeGreaterThan(nonSplitCommands);
    }

    [Fact(DisplayName = "GetPagedAsync specification without IKyrolusHasSplitQuery ignores policy split default")]
    public async Task GetPagedAsync_WithoutSplitInterface_IgnoresPolicySplitDefault()
    {
        var policy = new KyrolusRepositoryPolicy { UseSplitQueryDefault = true };
        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        var spec = new TestPagedSpecificationWithoutSplit<Product, Product>
        {
            Filter = x => x.Id == DataSeeder.productLaptopId,
            Selector = x => x,
            Includes = [x => x.Reviews, x => x.OrderLines, x => x.ProductCategories],
            AsNoTracking = true,
            PageNumber = 1,
            PageSize = 1
        };

        counter.Reset();
        _ = await repo.GetPagedAsync(spec);
        counter.Count.ShouldBe(2);
    }

    [Theory(DisplayName = "GetPagedAsync IncludeDeleted controls visibility of soft-deleted entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetPagedAsync_IncludeDeleted_Works(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (compositeKey)
        {
            var review = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "paged-soft-composite");
            await SeedReviewAsync(review);
            try
            {
                await SoftDeleteReviewAsync(review.ProductId, review.CustomerId);
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

                var excludedSpec = new TestPagedSpecification<Review, Review>
                {
                    Filter = x => x.ProductId == review.ProductId && x.CustomerId == review.CustomerId,
                    Selector = x => x,
                    IncludeDeleted = false,
                    AsNoTracking = true,
                    UseSplitQuery = false,
                    PageNumber = 1,
                    PageSize = 5
                };
                var includedSpec = new TestPagedSpecification<Review, Review>
                {
                    Filter = x => x.ProductId == review.ProductId && x.CustomerId == review.CustomerId,
                    Selector = x => x,
                    IncludeDeleted = true,
                    AsNoTracking = true,
                    UseSplitQuery = false,
                    PageNumber = 1,
                    PageSize = 5
                };

                var (excludedItems, excludedTotal) = await repo.GetPagedAsync(excludedSpec);
                excludedItems.ShouldBeEmpty();
                excludedTotal.ShouldBe(0);

                var (includedItems, includedTotal) = await repo.GetPagedAsync(includedSpec);
                includedItems.Count.ShouldBe(1);
                includedTotal.ShouldBe(1);
            }
            finally
            {
                await CleanupReviewAsync(review.ProductId, review.CustomerId);
            }
            return;
        }

        var product = CreateValidProduct(name: "paged-soft-single");
        await SeedProductAsync(product);
        try
        {
            await SoftDeleteProductAsync(product.Id);
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

            var excludedSpec = new TestPagedSpecification<Product, Product>
            {
                Filter = x => x.Id == product.Id,
                Selector = x => x,
                IncludeDeleted = false,
                AsNoTracking = true,
                UseSplitQuery = false,
                PageNumber = 1,
                PageSize = 5
            };
            var includedSpec = new TestPagedSpecification<Product, Product>
            {
                Filter = x => x.Id == product.Id,
                Selector = x => x,
                IncludeDeleted = true,
                AsNoTracking = true,
                UseSplitQuery = false,
                PageNumber = 1,
                PageSize = 5
            };

            var (excludedItems, excludedTotal) = await repo.GetPagedAsync(excludedSpec);
            excludedItems.ShouldBeEmpty();
            excludedTotal.ShouldBe(0);

            var (includedItems, includedTotal) = await repo.GetPagedAsync(includedSpec);
            includedItems.Count.ShouldBe(1);
            includedTotal.ShouldBe(1);
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "GetPagedAsync respects global filters")]
    [MemberData(nameof(GlobalFilterCases))]
    public async Task GetPagedAsync_GlobalFilters_Work(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = GlobalFilterSpecs[caseId];
        var customFactory = WithPolicy(spec.Policy);
        using var scope = customFactory.Services.CreateScope();

        if (spec.IsComposite)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var pagedSpec = new TestPagedSpecification<Review, Review>
            {
                Filter = x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId,
                Selector = x => x,
                AsNoTracking = true,
                UseSplitQuery = false,
                PageNumber = 1,
                PageSize = 5
            };
            var (items, total) = await repo.GetPagedAsync(pagedSpec);
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
        var singleSpec = new TestPagedSpecification<Product, Product>
        {
            Filter = x => x.Id == DataSeeder.productLaptopId,
            Selector = x => x,
            AsNoTracking = true,
            UseSplitQuery = false,
            PageNumber = 1,
            PageSize = 5
        };
        var (singleItems, singleTotal) = await singleRepo.GetPagedAsync(singleSpec);
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
