namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.AddRangeAsyncTests;

public partial class AddRangeAsyncTests
{
    private sealed record AddRangeSingleKeySpec(Func<List<Product>> CreateEntities, Action<List<Product>> AssertPersisted);
    private sealed record AddRangeCompositeKeySpec(Func<List<Review>> CreateEntities, Action<List<Review>> AssertPersisted);

    private static readonly IReadOnlyDictionary<string, AddRangeSingleKeySpec> SingleKeySuccessSpecs = BuildSingleKeySuccessSpecs();
    private static readonly IReadOnlyDictionary<string, AddRangeCompositeKeySpec> CompositeKeySuccessSpecs = BuildCompositeKeySuccessSpecs();

    public static TheoryData<string> SingleKeySuccessCases => CaseIdsFrom(SingleKeySuccessSpecs);
    public static TheoryData<string> CompositeKeySuccessCases => CaseIdsFrom(CompositeKeySuccessSpecs);

    [Theory(DisplayName = "AddRangeAsync adds single-key entities")]
    [MemberData(nameof(SingleKeySuccessCases))]
    public async Task AddRangeAsync_SingleKey_AddsEntities(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SingleKeySuccessSpecs[caseId];
        var entities = spec.CreateEntities();

        try
        {
            var (response, content) = await PostEntityRangeAsync(entities);
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent, content);
            content.ShouldBeEmpty();

            var ids = entities.Select(x => x.Id).ToArray();

            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await db.Products.AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .OrderBy(x => x.Name)
                .ToListAsync();

            persisted.Count.ShouldBe(entities.Count);
            spec.AssertPersisted(persisted);
        }
        finally
        {
            await CleanupProductsAsync(entities.Select(x => x.Id));
        }
    }

    [Theory(DisplayName = "AddRangeAsync adds composite-key entities")]
    [MemberData(nameof(CompositeKeySuccessCases))]
    public async Task AddRangeAsync_CompositeKey_AddsEntities(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = CompositeKeySuccessSpecs[caseId];
        var entities = spec.CreateEntities();

        try
        {
            var (response, content) = await PostEntityRangeAsync(entities);
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent, content);
            content.ShouldBeEmpty();

            var keys = entities
                .Select(x => new { x.ProductId, x.CustomerId })
                .ToList();

            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await db.Reviews.AsNoTracking()
                .Where(x => keys.Select(k => k.ProductId).Contains(x.ProductId) && keys.Select(k => k.CustomerId).Contains(x.CustomerId))
                .ToListAsync();

            persisted = persisted
                .Where(x => keys.Any(k => k.ProductId == x.ProductId && k.CustomerId == x.CustomerId))
                .OrderBy(x => x.Rating)
                .ToList();

            persisted.Count.ShouldBe(entities.Count);
            spec.AssertPersisted(persisted);
        }
        finally
        {
            await CleanupReviewsAsync(entities.Select(x => (x.ProductId, x.CustomerId)));
        }
    }

    private static IReadOnlyDictionary<string, AddRangeSingleKeySpec> BuildSingleKeySuccessSpecs()
        => new Dictionary<string, AddRangeSingleKeySpec>
        {
            ["basic"] = new(
                CreateEntities: () =>
                [
                    CreateValidProduct(price: 99m, stockQuantity: 11, weight: 1.1m, count: 4),
                    CreateValidProduct(price: 199m, stockQuantity: 21, weight: 2.2m, count: 8)
                ],
                AssertPersisted: products =>
                {
                    products.Select(x => x.Price).OrderBy(x => x).ShouldBe([99m, 199m]);
                    products.All(x => x.IsDeleted == false).ShouldBeTrue();
                    products.All(x => x.Name.StartsWith("RangeProduct-")).ShouldBeTrue();
                }),

            ["nullable-values"] = new(
                CreateEntities: () =>
                [
                    CreateValidProduct(weight: null, count: null, addedAt: null),
                    CreateValidProduct(weight: 0.33m, count: 1, addedAt: new TimeOnly(17, 0))
                ],
                AssertPersisted: products =>
                {
                    products.Count(x => x.Weight is null).ShouldBe(1);
                    products.Any(x => x.AddedAt == new TimeOnly(17, 0)).ShouldBeTrue();
                    products.Any(x => x.Count is null).ShouldBeTrue();
                })
        };

    private static IReadOnlyDictionary<string, AddRangeCompositeKeySpec> BuildCompositeKeySuccessSpecs()
        => new Dictionary<string, AddRangeCompositeKeySpec>
        {
            ["basic"] = new(
                CreateEntities: () =>
                [
                    CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 1, comment: "Low score"),
                    CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 5, comment: "Great score")
                ],
                AssertPersisted: reviews =>
                {
                    reviews.Select(x => x.Rating).ShouldBe([1, 5]);
                    reviews.Any(x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJohnId).ShouldBeTrue();
                    reviews.Any(x => x.ProductId == DataSeeder.productHeadphonesId && x.CustomerId == DataSeeder.customerJaneId).ShouldBeTrue();
                }),

            ["nullable-fields"] = new(
                CreateEntities: () =>
                [
                    CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 3, comment: null, addedAt: null),
                    CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 4, comment: "With addedAt", addedAt: new TimeOnly(18, 30))
                ],
                AssertPersisted: reviews =>
                {
                    reviews.Any(x => x.Comment is null && x.AddedAt is null).ShouldBeTrue();
                    reviews.Any(x => x.AddedAt == new TimeOnly(18, 30)).ShouldBeTrue();
                })
        };
}
