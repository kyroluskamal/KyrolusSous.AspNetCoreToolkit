namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.RemoveAsyncTests;

public partial class RemoveAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private sealed record RemoveSingleApiSpec(Func<Product> SeedEntity, bool UseTryRoute, bool SoftDeleteFlag);
    private sealed record RemoveCompositeApiSpec(Func<Review> SeedEntity, bool UseTryRoute, bool SoftDeleteFlag);
    private sealed record RemoveRangeSingleApiSpec(Func<List<Product>> SeedEntities, bool SoftDeleteFlag);
    private sealed record RemoveRangeCompositeApiSpec(Func<List<Review>> SeedEntities, bool SoftDeleteFlag);

    private static readonly IReadOnlyDictionary<string, RemoveSingleApiSpec> RemoveSingleSpecs = BuildRemoveSingleSpecs();
    private static readonly IReadOnlyDictionary<string, RemoveCompositeApiSpec> RemoveCompositeSpecs = BuildRemoveCompositeSpecs();
    private static readonly IReadOnlyDictionary<string, RemoveRangeSingleApiSpec> RemoveRangeSingleSpecs = BuildRemoveRangeSingleSpecs();
    private static readonly IReadOnlyDictionary<string, RemoveRangeCompositeApiSpec> RemoveRangeCompositeSpecs = BuildRemoveRangeCompositeSpecs();

    public static TheoryData<string> RemoveSingleCases => CaseIdsFrom(RemoveSingleSpecs);
    public static TheoryData<string> RemoveCompositeCases => CaseIdsFrom(RemoveCompositeSpecs);
    public static TheoryData<string> RemoveRangeSingleCases => CaseIdsFrom(RemoveRangeSingleSpecs);
    public static TheoryData<string> RemoveRangeCompositeCases => CaseIdsFrom(RemoveRangeCompositeSpecs);

    [Theory(DisplayName = "RemoveAsync API deletes single-key entities")]
    [MemberData(nameof(RemoveSingleCases))]
    public async Task RemoveAsync_Api_SingleKey_DeletesEntity(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = RemoveSingleSpecs[caseId];
        var entity = spec.SeedEntity();
        await SeedProductAsync(entity);

        try
        {
            var (response, content) = await DeleteSingleKeyAsync<Product>(
                entity.Id,
                softDelete: spec.SoftDeleteFlag,
                useTryRoute: spec.UseTryRoute);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent, content);
            content.ShouldBeEmpty();

            var exists = await ProductExistsAsync(entity.Id);
            exists.ShouldBeFalse();
        }
        finally
        {
            await CleanupProductAsync(entity.Id);
        }
    }

    [Theory(DisplayName = "RemoveAsync API deletes composite-key entities")]
    [MemberData(nameof(RemoveCompositeCases))]
    public async Task RemoveAsync_Api_CompositeKey_DeletesEntity(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = RemoveCompositeSpecs[caseId];
        var entity = spec.SeedEntity();
        await SeedReviewAsync(entity);

        try
        {
            var (response, content) = await DeleteCompositeKeyAsync<Review>(
                [entity.ProductId, entity.CustomerId],
                softDelete: spec.SoftDeleteFlag,
                useTryRoute: spec.UseTryRoute);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent, content);
            content.ShouldBeEmpty();

            var exists = await ReviewExistsAsync(entity.ProductId, entity.CustomerId);
            exists.ShouldBeFalse();
        }
        finally
        {
            await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
        }
    }

    [Theory(DisplayName = "RemoveRangeAsync API deletes single-key entities")]
    [MemberData(nameof(RemoveRangeSingleCases))]
    public async Task RemoveRangeAsync_Api_SingleKey_DeletesEntities(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = RemoveRangeSingleSpecs[caseId];
        var entities = spec.SeedEntities();
        await SeedProductsAsync(entities);

        try
        {
            var (response, content) = await DeleteEntityRangeAsync(entities, softDelete: spec.SoftDeleteFlag);
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent, content);
            content.ShouldBeEmpty();

            foreach (var entity in entities)
                (await ProductExistsAsync(entity.Id)).ShouldBeFalse();
        }
        finally
        {
            await CleanupProductsAsync(entities.Select(x => x.Id));
        }
    }

    [Theory(DisplayName = "RemoveRangeAsync API deletes composite-key entities")]
    [MemberData(nameof(RemoveRangeCompositeCases))]
    public async Task RemoveRangeAsync_Api_CompositeKey_DeletesEntities(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = RemoveRangeCompositeSpecs[caseId];
        var entities = spec.SeedEntities();
        await SeedReviewsAsync(entities);

        try
        {
            var (response, content) = await DeleteEntityRangeAsync(entities, softDelete: spec.SoftDeleteFlag);
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent, content);
            content.ShouldBeEmpty();

            foreach (var entity in entities)
                (await ReviewExistsAsync(entity.ProductId, entity.CustomerId)).ShouldBeFalse();
        }
        finally
        {
            await CleanupReviewsAsync(entities.Select(x => (x.ProductId, x.CustomerId)));
        }
    }

    private static IReadOnlyDictionary<string, RemoveSingleApiSpec> BuildRemoveSingleSpecs()
        => new Dictionary<string, RemoveSingleApiSpec>
        {
            ["delete-route-softdelete-false"] = new(
                SeedEntity: () => CreateValidProduct(name: "remove-delete-route"),
                UseTryRoute: false,
                SoftDeleteFlag: false),

            ["delete-route-softdelete-true"] = new(
                SeedEntity: () => CreateValidProduct(name: "remove-delete-route-soft"),
                UseTryRoute: false,
                SoftDeleteFlag: true),

            ["try-route-softdelete-false"] = new(
                SeedEntity: () => CreateValidProduct(name: "remove-try-route"),
                UseTryRoute: true,
                SoftDeleteFlag: false)
        };

    private static IReadOnlyDictionary<string, RemoveCompositeApiSpec> BuildRemoveCompositeSpecs()
        => new Dictionary<string, RemoveCompositeApiSpec>
        {
            ["delete-route-softdelete-false"] = new(
                SeedEntity: () => CreateValidReview(
                    productId: DataSeeder.productBookId,
                    customerId: DataSeeder.customerJohnId,
                    rating: 2,
                    comment: "remove composite"),
                UseTryRoute: false,
                SoftDeleteFlag: false),

            ["delete-route-softdelete-true"] = new(
                SeedEntity: () => CreateValidReview(
                    productId: DataSeeder.productLaptopId,
                    customerId: DataSeeder.customerJohnId,
                    rating: 3,
                    comment: "remove composite soft"),
                UseTryRoute: false,
                SoftDeleteFlag: true),

            ["try-route-softdelete-false"] = new(
                SeedEntity: () => CreateValidReview(
                    productId: DataSeeder.productHeadphonesId,
                    customerId: DataSeeder.customerJaneId,
                    rating: 4,
                    comment: "remove composite try"),
                UseTryRoute: true,
                SoftDeleteFlag: false)
        };

    private static IReadOnlyDictionary<string, RemoveRangeSingleApiSpec> BuildRemoveRangeSingleSpecs()
        => new Dictionary<string, RemoveRangeSingleApiSpec>
        {
            ["remove-range-softdelete-false"] = new(
                SeedEntities: () =>
                [
                    CreateValidProduct(name: "range-single-1"),
                    CreateValidProduct(name: "range-single-2")
                ],
                SoftDeleteFlag: false),

            ["remove-range-softdelete-true"] = new(
                SeedEntities: () =>
                [
                    CreateValidProduct(name: "range-single-soft-1"),
                    CreateValidProduct(name: "range-single-soft-2")
                ],
                SoftDeleteFlag: true)
        };

    private static IReadOnlyDictionary<string, RemoveRangeCompositeApiSpec> BuildRemoveRangeCompositeSpecs()
        => new Dictionary<string, RemoveRangeCompositeApiSpec>
        {
            ["remove-range-softdelete-false"] = new(
                SeedEntities: () =>
                [
                    CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 2, comment: "range-comp-1"),
                    CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 4, comment: "range-comp-2")
                ],
                SoftDeleteFlag: false),

            ["remove-range-softdelete-true"] = new(
                SeedEntities: () =>
                [
                    CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 3, comment: "range-comp-soft-1"),
                    CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 5, comment: "range-comp-soft-2")
                ],
                SoftDeleteFlag: true)
        };
}
