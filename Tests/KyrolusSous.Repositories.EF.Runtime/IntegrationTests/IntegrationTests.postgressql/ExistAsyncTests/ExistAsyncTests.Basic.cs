using System.Linq.Expressions;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.ExistAsyncTests;

public partial class ExistAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    public static TheoryData<string, Expression<Func<Product, bool>>, bool> SingleRepoCases => new()
    {
        { "single-match-by-sku", x => x.Sku == "LP-15", true },
        { "single-miss-by-sku", x => x.Sku == "NO-SUCH-SKU", false },
        { "single-nullable-weight", x => x.Weight == null, true }
    };

    public static TheoryData<string, Expression<Func<Review, bool>>, bool> CompositeRepoCases => new()
    {
        { "composite-match-by-rating", x => x.Rating == 5, true },
        { "composite-miss-by-rating", x => x.Rating == 999, false },
        { "composite-match-by-key", x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId, true }
    };

    public static TheoryData<string, bool> KeyTypeCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "ExistAsync returns expected result for single-key filters")]
    [MemberData(nameof(SingleRepoCases))]
    public async Task ExistAsync_SingleKey_Basic_Works(string caseId, Expression<Func<Product, bool>> filter, bool expected)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var exists = await repo.ExistAsync(filter);
        exists.ShouldBe(expected);
    }

    [Theory(DisplayName = "ExistAsync returns expected result for composite-key filters")]
    [MemberData(nameof(CompositeRepoCases))]
    public async Task ExistAsync_CompositeKey_Basic_Works(string caseId, Expression<Func<Review, bool>> filter, bool expected)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        var exists = await repo.ExistAsync(filter);
        exists.ShouldBe(expected);
    }

    [Theory(DisplayName = "ExistAsync excludes soft-deleted entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task ExistAsync_SoftDeletedEntity_ReturnsFalse(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (compositeKey)
        {
            var review = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "exists-soft-composite");
            await SeedReviewAsync(review);

            try
            {
                await SoftDeleteReviewAsync(review.ProductId, review.CustomerId);

                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var exists = await repo.ExistAsync(x => x.ProductId == review.ProductId && x.CustomerId == review.CustomerId);
                exists.ShouldBeFalse();
            }
            finally
            {
                await CleanupReviewAsync(review.ProductId, review.CustomerId);
            }

            return;
        }

        var product = CreateValidProduct(name: "exists-soft-single");
        await SeedProductAsync(product);

        try
        {
            await SoftDeleteProductAsync(product.Id);

            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var exists = await repo.ExistAsync(x => x.Id == product.Id);
            exists.ShouldBeFalse();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    private sealed record GlobalFilterSpec(bool IsComposite, KyrolusRepositoryPolicy Policy, bool Expected);
    private static readonly IReadOnlyDictionary<string, GlobalFilterSpec> GlobalFilterSpecs = BuildGlobalFilterSpecs();
    public static TheoryData<string> GlobalFilterCases => CaseIdsFrom(GlobalFilterSpecs);

    [Theory(DisplayName = "ExistAsync respects global filters")]
    [MemberData(nameof(GlobalFilterCases))]
    public async Task ExistAsync_GlobalFilter_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = GlobalFilterSpecs[caseId];
        var customFactory = WithPolicy(spec.Policy);
        using var scope = customFactory.Services.CreateScope();

        if (spec.IsComposite)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var exists = await repo.ExistAsync(x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId);
            exists.ShouldBe(spec.Expected);
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleExists = await singleRepo.ExistAsync(x => x.Id == DataSeeder.productLaptopId);
        singleExists.ShouldBe(spec.Expected);
    }

    public static TheoryData<string, bool, QueryRequest?, bool> ApiCases => new()
    {
        {
            "api-single-match",
            false,
            new QueryRequest(Filters:
            [
                new FilterClause("Sku", "eq", "LP-15")
            ]),
            true
        },
        {
            "api-single-miss",
            false,
            new QueryRequest(Filters:
            [
                new FilterClause("Sku", "eq", "NO-SUCH-SKU")
            ]),
            false
        },
        {
            "api-composite-match",
            true,
            new QueryRequest(Filters:
            [
                new FilterClause("Rating", "eq", "5")
            ]),
            true
        },
        {
            "api-composite-miss",
            true,
            new QueryRequest(Filters:
            [
                new FilterClause("Rating", "eq", "999")
            ]),
            false
        },
        {
            "api-no-request-defaults-true",
            false,
            null,
            true
        }
    };

    [Theory(DisplayName = "Exist API returns expected result")]
    [MemberData(nameof(ApiCases))]
    public async Task ExistAsync_Api_Works(string caseId, bool compositeKey, QueryRequest? request, bool expected)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (compositeKey)
        {
            var (response, exists, content) = await GetExistsAsync<Review>(request);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, content);
            exists.ShouldNotBeNull();
            exists!.Value.ShouldBe(expected);
            return;
        }

        var (singleResponse, singleExists, singleContent) = await GetExistsAsync<Product>(request);
        singleResponse.StatusCode.ShouldBe(HttpStatusCode.OK, singleContent);
        singleExists.ShouldNotBeNull();
        singleExists!.Value.ShouldBe(expected);
    }

    [Theory(DisplayName = "Exist API excludes soft-deleted entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task ExistAsync_Api_SoftDeletedEntity_ReturnsFalse(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (compositeKey)
        {
            var review = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 4, comment: "api-soft-composite");
            await SeedReviewAsync(review);

            try
            {
                await SoftDeleteReviewAsync(review.ProductId, review.CustomerId);
                var request = new QueryRequest(Filters:
                [
                    new FilterClause("ProductId", "eq", review.ProductId.ToString()),
                    new FilterClause("CustomerId", "eq", review.CustomerId.ToString())
                ]);
                var (response, exists, content) = await GetExistsAsync<Review>(request);
                response.StatusCode.ShouldBe(HttpStatusCode.OK, content);
                exists.ShouldNotBeNull();
                exists!.Value.ShouldBeFalse();
            }
            finally
            {
                await CleanupReviewAsync(review.ProductId, review.CustomerId);
            }

            return;
        }

        var product = CreateValidProduct(name: "api-soft-single");
        await SeedProductAsync(product);

        try
        {
            await SoftDeleteProductAsync(product.Id);
            var request = new QueryRequest(Filters:
            [
                new FilterClause("Id", "eq", product.Id.ToString())
            ]);
            var (response, exists, content) = await GetExistsAsync<Product>(request);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, content);
            exists.ShouldNotBeNull();
            exists!.Value.ShouldBeFalse();
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
                Expected: false),
            ["single-allowed"] = new(
                IsComposite: false,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(x => x.Price >= 1000m),
                Expected: true),
            ["composite-blocked"] = new(
                IsComposite: true,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Review>(x => x.Rating < 5),
                Expected: false),
            ["composite-allowed"] = new(
                IsComposite: true,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Review>(x => x.Rating <= 5),
                Expected: true)
        };
}
