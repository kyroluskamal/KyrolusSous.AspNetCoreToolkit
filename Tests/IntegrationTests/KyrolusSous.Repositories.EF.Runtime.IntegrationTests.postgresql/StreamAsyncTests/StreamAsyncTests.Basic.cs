using System.Linq.Expressions;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.StreamAsyncTests;

public partial class StreamAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    public static TheoryData<string, Expression<Func<Product, bool>>, string[]> SingleStreamCases => new()
    {
        {
            "single-order-by-price-asc",
            x => x.Price >= 199m,
            ["NC-100", "LP-15"]
        },
        {
            "single-filter-by-sku",
            x => x.Sku == "BOOK-CC",
            ["BOOK-CC"]
        }
    };

    public static TheoryData<string, Expression<Func<Review, bool>>, int[]> CompositeStreamCases => new()
    {
        {
            "composite-order-by-rating-desc",
            x => x.Rating >= 4,
            [5, 4]
        },
        {
            "composite-filter-by-customer",
            x => x.CustomerId == DataSeeder.customerJaneId,
            [5, 4]
        }
    };

    public static TheoryData<string, bool, bool, bool> TrackingCases => new()
    {
        { "single-asnotracking-true", false, true, false },
        { "single-asnotracking-false", false, false, true },
        { "composite-asnotracking-true", true, true, false },
        { "composite-asnotracking-false", true, false, true }
    };

    public static TheoryData<string, bool> KeyTypeCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "StreamAsync returns expected single-key results")]
    [MemberData(nameof(SingleStreamCases))]
    public async Task StreamAsync_SingleKey_FilterAndOrder_Works(string caseId, Expression<Func<Product, bool>> filter, string[] expectedSkus)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var items = await CollectAsync(repo.StreamAsync(filter, q => q.OrderBy(x => x.Price), asNoTracking: true, useSplitQuery: false));

        items.Select(x => x.Sku).ShouldBe(expectedSkus);
    }

    [Theory(DisplayName = "StreamAsync returns expected composite-key results")]
    [MemberData(nameof(CompositeStreamCases))]
    public async Task StreamAsync_CompositeKey_FilterAndOrder_Works(string caseId, Expression<Func<Review, bool>> filter, int[] expectedRatings)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
        var items = await CollectAsync(repo.StreamAsync(filter, q => q.OrderByDescending(x => x.Rating), asNoTracking: true, useSplitQuery: false));

        items.Select(x => x.Rating).ShouldBe(expectedRatings);
    }

    [Fact(DisplayName = "StreamAsync supports include expressions for single-key entities")]
    public async Task StreamAsync_SingleKey_IncludeExpressions_Work()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var items = await CollectAsync(repo.StreamAsync(
            x => x.Id == DataSeeder.productLaptopId,
            asNoTracking: false,
            useSplitQuery: false,
            includeExpressions:
            [
                x => x.Reviews,
                x => x.Store
            ]));

        items.Count.ShouldBe(1);
        items[0].Reviews.ShouldNotBeEmpty();
        items[0].Store.ShouldNotBeNull();
    }

    [Fact(DisplayName = "StreamAsync supports include expressions for composite-key entities")]
    public async Task StreamAsync_CompositeKey_IncludeExpressions_Work()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
        var items = await CollectAsync(repo.StreamAsync(
            x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId,
            asNoTracking: false,
            useSplitQuery: false,
            includeExpressions:
            [
                x => x.Product,
                x => x.Customer
            ]));

        items.Count.ShouldBe(1);
        items[0].Product.ShouldNotBeNull();
        items[0].Customer.ShouldNotBeNull();
    }

    [Theory(DisplayName = "StreamAsync respects AsNoTracking settings")]
    [MemberData(nameof(TrackingCases))]
    public async Task StreamAsync_AsNoTracking_Works(string caseId, bool compositeKey, bool asNoTracking, bool expectTracked)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ChangeTracker.Clear();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            _ = await CollectAsync(repo.StreamAsync(
                x => x.ProductId == DataSeeder.productLaptopId,
                asNoTracking: asNoTracking,
                useSplitQuery: false));
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            _ = await CollectAsync(repo.StreamAsync(
                x => x.Id == DataSeeder.productLaptopId,
                asNoTracking: asNoTracking,
                useSplitQuery: false));
        }

        if (expectTracked)
            db.ChangeTracker.Entries().ShouldNotBeEmpty();
        else
            db.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact(DisplayName = "StreamAsync UseSplitQuery increases SQL commands with collection includes")]
    public async Task StreamAsync_UseSplitQuery_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        counter.Reset();
        var nonSplit = await CollectAsync(repo.StreamAsync(
            x => x.Id == DataSeeder.productLaptopId,
            asNoTracking: true,
            useSplitQuery: false,
            includeExpressions:
            [
                x => x.Reviews,
                x => x.OrderLines,
                x => x.ProductCategories
            ]));
        nonSplit.Count.ShouldBe(1);
        var nonSplitCommands = counter.Count;

        counter.Reset();
        var split = await CollectAsync(repo.StreamAsync(
            x => x.Id == DataSeeder.productLaptopId,
            asNoTracking: true,
            useSplitQuery: true,
            includeExpressions:
            [
                x => x.Reviews,
                x => x.OrderLines,
                x => x.ProductCategories
            ]));
        split.Count.ShouldBe(1);
        var splitCommands = counter.Count;

        nonSplitCommands.ShouldBeGreaterThan(0);
        splitCommands.ShouldBeGreaterThan(nonSplitCommands);
    }

    [Theory(DisplayName = "StreamAsync excludes soft-deleted entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task StreamAsync_SoftDeletedEntity_ReturnsEmpty(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (compositeKey)
        {
            var review = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 3, comment: "stream-soft-composite");
            await SeedReviewAsync(review);

            try
            {
                await SoftDeleteReviewAsync(review.ProductId, review.CustomerId);
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var items = await CollectAsync(repo.StreamAsync(x => x.ProductId == review.ProductId && x.CustomerId == review.CustomerId));
                items.ShouldBeEmpty();
            }
            finally
            {
                await CleanupReviewAsync(review.ProductId, review.CustomerId);
            }

            return;
        }

        var product = CreateValidProduct(name: "stream-soft-single");
        await SeedProductAsync(product);

        try
        {
            await SoftDeleteProductAsync(product.Id);
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var items = await CollectAsync(repo.StreamAsync(x => x.Id == product.Id));
            items.ShouldBeEmpty();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    private sealed record GlobalFilterSpec(bool IsComposite, KyrolusRepositoryPolicy Policy, bool ExpectedFound);
    private static readonly IReadOnlyDictionary<string, GlobalFilterSpec> GlobalFilterSpecs = BuildGlobalFilterSpecs();
    public static TheoryData<string> GlobalFilterCases => CaseIdsFrom(GlobalFilterSpecs);

    [Theory(DisplayName = "StreamAsync respects global filters")]
    [MemberData(nameof(GlobalFilterCases))]
    public async Task StreamAsync_GlobalFilters_Work(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = GlobalFilterSpecs[caseId];
        var customFactory = WithPolicy(spec.Policy);
        using var scope = customFactory.Services.CreateScope();

        if (spec.IsComposite)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var items = await CollectAsync(repo.StreamAsync(x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId));
            if (spec.ExpectedFound)
                items.Count.ShouldBe(1);
            else
                items.ShouldBeEmpty();
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleItems = await CollectAsync(singleRepo.StreamAsync(x => x.Id == DataSeeder.productLaptopId));
        if (spec.ExpectedFound)
            singleItems.Count.ShouldBe(1);
        else
            singleItems.ShouldBeEmpty();
    }

    public static TheoryData<string, bool, QueryRequest?, int> ApiCases => new()
    {
        {
            "api-single-match",
            false,
            new QueryRequest(Filters:
            [
                new FilterClause("Sku", "eq", "LP-15")
            ]),
            1
        },
        {
            "api-single-miss",
            false,
            new QueryRequest(Filters:
            [
                new FilterClause("Sku", "eq", "NO-SUCH-SKU")
            ]),
            0
        },
        {
            "api-composite-match",
            true,
            new QueryRequest(Filters:
            [
                new FilterClause("Rating", "eq", "5")
            ]),
            1
        },
        {
            "api-composite-miss",
            true,
            new QueryRequest(Filters:
            [
                new FilterClause("Rating", "eq", "999")
            ]),
            0
        }
    };

    [Theory(DisplayName = "Stream API returns expected results")]
    [MemberData(nameof(ApiCases))]
    public async Task StreamAsync_Api_Works(string caseId, bool compositeKey, QueryRequest? request, int expectedCount)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (compositeKey)
        {
            var (response, items, content) = await GetStreamAsync<Review>(request);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, content);
            items.ShouldNotBeNull();
            items!.Count.ShouldBe(expectedCount);
            return;
        }

        var (singleResponse, singleItems, singleContent) = await GetStreamAsync<Product>(request);
        singleResponse.StatusCode.ShouldBe(HttpStatusCode.OK, singleContent);
        singleItems.ShouldNotBeNull();
        singleItems!.Count.ShouldBe(expectedCount);
    }

    [Theory(DisplayName = "Stream API excludes soft-deleted entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task StreamAsync_Api_SoftDeletedEntity_ReturnsEmpty(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (compositeKey)
        {
            var review = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 4, comment: "api-stream-soft-composite");
            await SeedReviewAsync(review);

            try
            {
                await SoftDeleteReviewAsync(review.ProductId, review.CustomerId);
                var request = new QueryRequest(Filters:
                [
                    new FilterClause("ProductId", "eq", review.ProductId.ToString()),
                    new FilterClause("CustomerId", "eq", review.CustomerId.ToString())
                ]);
                var (response, items, content) = await GetStreamAsync<Review>(request);
                response.StatusCode.ShouldBe(HttpStatusCode.OK, content);
                items.ShouldNotBeNull();
                items!.ShouldBeEmpty();
            }
            finally
            {
                await CleanupReviewAsync(review.ProductId, review.CustomerId);
            }

            return;
        }

        var product = CreateValidProduct(name: "api-stream-soft-single");
        await SeedProductAsync(product);

        try
        {
            await SoftDeleteProductAsync(product.Id);
            var request = new QueryRequest(Filters:
            [
                new FilterClause("Id", "eq", product.Id.ToString())
            ]);
            var (response, items, content) = await GetStreamAsync<Product>(request);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, content);
            items.ShouldNotBeNull();
            items!.ShouldBeEmpty();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
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
