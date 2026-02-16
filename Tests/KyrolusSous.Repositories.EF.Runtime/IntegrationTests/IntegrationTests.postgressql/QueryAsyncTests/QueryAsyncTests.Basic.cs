using System.Linq.Expressions;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.QueryAsyncTests;

public partial class QueryAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private sealed record SingleProjectionSpec(
        Expression<Func<Product, bool>>? Filter,
        Expression<Func<Product, ProductQueryProjection>> Selector,
        Func<IQueryable<Product>, IOrderedQueryable<Product>>? OrderBy,
        bool? AsNoTracking,
        bool? UseSplitQuery,
        Expression<Func<Product, object?>>[] Includes,
        Action<List<ProductQueryProjection>> AssertResult);

    private sealed record CompositeProjectionSpec(
        Expression<Func<Review, bool>>? Filter,
        Expression<Func<Review, ReviewQueryProjection>> Selector,
        Func<IQueryable<Review>, IOrderedQueryable<Review>>? OrderBy,
        bool? AsNoTracking,
        bool? UseSplitQuery,
        Expression<Func<Review, object?>>[] Includes,
        Action<List<ReviewQueryProjection>> AssertResult);

    private static readonly IReadOnlyDictionary<string, SingleProjectionSpec> SingleProjectionSpecs = BuildSingleProjectionSpecs();
    private static readonly IReadOnlyDictionary<string, CompositeProjectionSpec> CompositeProjectionSpecs = BuildCompositeProjectionSpecs();
    private static readonly IReadOnlyDictionary<string, GlobalFilterSpec> GlobalFilterSpecs = BuildGlobalFilterSpecs();

    public static TheoryData<string> SingleProjectionCases => CaseIdsFrom(SingleProjectionSpecs);
    public static TheoryData<string> CompositeProjectionCases => CaseIdsFrom(CompositeProjectionSpecs);
    public static TheoryData<string> GlobalFilterCases => CaseIdsFrom(GlobalFilterSpecs);
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

    [Theory(DisplayName = "QueryAsync overload returns expected single-key projections")]
    [MemberData(nameof(SingleProjectionCases))]
    public async Task QueryAsync_Overload_SingleKey_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SingleProjectionSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var items = await repo.QueryAsync(
            spec.Filter,
            spec.Selector,
            spec.OrderBy,
            spec.AsNoTracking,
            spec.UseSplitQuery,
            default,
            spec.Includes);

        spec.AssertResult(items);
    }

    [Theory(DisplayName = "QueryAsync overload returns expected composite-key projections")]
    [MemberData(nameof(CompositeProjectionCases))]
    public async Task QueryAsync_Overload_CompositeKey_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = CompositeProjectionSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        var items = await repo.QueryAsync(
            spec.Filter,
            spec.Selector,
            spec.OrderBy,
            spec.AsNoTracking,
            spec.UseSplitQuery,
            default,
            spec.Includes);

        spec.AssertResult(items);
    }

    [Theory(DisplayName = "QueryAsync overload respects AsNoTracking")]
    [MemberData(nameof(TrackingCases))]
    public async Task QueryAsync_Overload_AsNoTracking_Works(string caseId, bool compositeKey, bool asNoTracking)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ChangeTracker.Clear();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            _ = await repo.QueryAsync(
                x => x.ProductId == DataSeeder.productLaptopId,
                x => x,
                asNoTracking: asNoTracking,
                useSplitQuery: false);
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            _ = await repo.QueryAsync(
                x => x.Id == DataSeeder.productLaptopId,
                x => x,
                asNoTracking: asNoTracking,
                useSplitQuery: false);
        }

        if (asNoTracking)
            db.ChangeTracker.Entries().ShouldBeEmpty();
        else
            db.ChangeTracker.Entries().ShouldNotBeEmpty();
    }

    [Fact(DisplayName = "QueryAsync overload UseSplitQuery increases SQL commands with collection includes")]
    public async Task QueryAsync_Overload_UseSplitQuery_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        var nonSplit = await repo.QueryAsync(
            x => x.Id == DataSeeder.productLaptopId,
            x => x,
            asNoTracking: true,
            useSplitQuery: false,
            includeExpressions:
            [
                x => x.Reviews,
                x => x.OrderLines,
                x => x.ProductCategories
            ]);
        nonSplit.Count.ShouldBe(1);
        var nonSplitCommands = counter.Count;

        counter.Reset();
        var split = await repo.QueryAsync(
            x => x.Id == DataSeeder.productLaptopId,
            x => x,
            asNoTracking: true,
            useSplitQuery: true,
            includeExpressions:
            [
                x => x.Reviews,
                x => x.OrderLines,
                x => x.ProductCategories
            ]);
        split.Count.ShouldBe(1);
        var splitCommands = counter.Count;

        nonSplitCommands.ShouldBeGreaterThan(0);
        splitCommands.ShouldBeGreaterThan(nonSplitCommands);
    }

    [Theory(DisplayName = "QueryAsync overload excludes soft-deleted entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task QueryAsync_Overload_SoftDelete_Excluded(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (compositeKey)
        {
            var review = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 3, comment: "query-soft-composite");
            await SeedReviewAsync(review);
            try
            {
                await SoftDeleteReviewAsync(review.ProductId, review.CustomerId);
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var items = await repo.QueryAsync(
                    x => x.ProductId == review.ProductId && x.CustomerId == review.CustomerId,
                    x => x);
                items.ShouldBeEmpty();
            }
            finally
            {
                await CleanupReviewAsync(review.ProductId, review.CustomerId);
            }
            return;
        }

        var product = CreateValidProduct(name: "query-soft-single");
        await SeedProductAsync(product);
        try
        {
            await SoftDeleteProductAsync(product.Id);
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var items = await repo.QueryAsync(x => x.Id == product.Id, x => x);
            items.ShouldBeEmpty();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    private sealed record GlobalFilterSpec(bool IsComposite, KyrolusRepositoryPolicy Policy, bool ExpectedFound);

    [Theory(DisplayName = "QueryAsync overload respects global filters")]
    [MemberData(nameof(GlobalFilterCases))]
    public async Task QueryAsync_Overload_GlobalFilters_Work(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = GlobalFilterSpecs[caseId];
        var customFactory = WithPolicy(spec.Policy);
        using var scope = customFactory.Services.CreateScope();

        if (spec.IsComposite)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var items = await repo.QueryAsync(x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId, x => x);
            if (spec.ExpectedFound)
                items.Count.ShouldBe(1);
            else
                items.ShouldBeEmpty();
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleItems = await singleRepo.QueryAsync(x => x.Id == DataSeeder.productLaptopId, x => x);
        if (spec.ExpectedFound)
            singleItems.Count.ShouldBe(1);
        else
            singleItems.ShouldBeEmpty();
    }

    private static IReadOnlyDictionary<string, SingleProjectionSpec> BuildSingleProjectionSpecs()
        => new Dictionary<string, SingleProjectionSpec>
        {
            ["filter-order-projection"] = new(
                Filter: x => x.Price >= 199m,
                Selector: x => new ProductQueryProjection(x.Id, x.Sku, x.Price, x.Reviews.Count),
                OrderBy: q => q.OrderBy(x => x.Price),
                AsNoTracking: true,
                UseSplitQuery: false,
                Includes: [],
                AssertResult: items =>
                {
                    items.Select(x => x.Sku).ShouldBe(["NC-100", "LP-15"]);
                    items.Select(x => x.Price).ShouldBe([199m, 1200m]);
                }),
            ["include-store"] = new(
                Filter: x => x.Id == DataSeeder.productLaptopId,
                Selector: x => new ProductQueryProjection(x.Id, x.Sku, x.Price, x.Reviews.Count),
                OrderBy: null,
                AsNoTracking: false,
                UseSplitQuery: false,
                Includes: [x => x.Store],
                AssertResult: items =>
                {
                    items.Count.ShouldBe(1);
                    items[0].Sku.ShouldBe("LP-15");
                })
        };

    private static IReadOnlyDictionary<string, CompositeProjectionSpec> BuildCompositeProjectionSpecs()
        => new Dictionary<string, CompositeProjectionSpec>
        {
            ["filter-order-projection"] = new(
                Filter: x => x.Rating >= 3,
                Selector: x => new ReviewQueryProjection(x.ProductId, x.CustomerId, x.Rating, x.Comment),
                OrderBy: q => q.OrderByDescending(x => x.Rating),
                AsNoTracking: true,
                UseSplitQuery: false,
                Includes: [],
                AssertResult: items =>
                {
                    items.Select(x => x.Rating).ShouldBe([5, 4, 3]);
                }),
            ["include-navigations"] = new(
                Filter: x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId,
                Selector: x => new ReviewQueryProjection(x.ProductId, x.CustomerId, x.Rating, x.Comment),
                OrderBy: null,
                AsNoTracking: false,
                UseSplitQuery: false,
                Includes: [x => x.Product, x => x.Customer],
                AssertResult: items =>
                {
                    items.Count.ShouldBe(1);
                    items[0].Rating.ShouldBe(5);
                })
        };

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
