namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryRestoreAsyncTests;

public partial class TryRestoreAsyncTests
{
    private sealed record ApiSuccessSpec(bool IsComposite);
    private static readonly IReadOnlyDictionary<string, ApiSuccessSpec> ApiSuccessSpecs = BuildApiSuccessSpecs();
    public static TheoryData<string> ApiSuccessCases => CaseIdsFrom(ApiSuccessSpecs);

    [Theory(DisplayName = "TryRestoreAsync API returns no-content for restorable entities")]
    [MemberData(nameof(ApiSuccessCases))]
    public async Task TryRestoreAsync_Api_Success(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = ApiSuccessSpecs[caseId];

        if (spec.IsComposite)
        {
            var entity = CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 4, comment: $"try-restore-api-composite-{Guid.NewGuid():N}");
            await SeedReviewAsync(entity);

            try
            {
                await SoftDeleteReviewAsync(entity.ProductId, entity.CustomerId);
                var (response, content) = await PostCompositeTryRestoreAsync<Review>([entity.ProductId, entity.CustomerId]);

                response.StatusCode.ShouldBe(HttpStatusCode.NoContent, content);
                content.ShouldBeEmpty();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var product = CreateValidProduct(name: $"try-restore-api-single-{Guid.NewGuid():N}");
        await SeedProductAsync(product);

        try
        {
            await SoftDeleteProductAsync(product.Id);
            var (response, content) = await PostSingleTryRestoreAsync<Product>(product.Id);

            response.StatusCode.ShouldBe(HttpStatusCode.NoContent, content);
            content.ShouldBeEmpty();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    private sealed record InvalidTryRestoreApiSpec(
        string Route,
        HttpStatusCode ExpectedStatus,
        string? MessageContains = null);

    private static readonly IReadOnlyDictionary<string, InvalidTryRestoreApiSpec> InvalidApiSpecs = BuildInvalidApiSpecs();
    public static TheoryData<string> InvalidApiCases => CaseIdsFrom(InvalidApiSpecs);

    [Theory(DisplayName = "TryRestoreAsync API rejects invalid requests")]
    [MemberData(nameof(InvalidApiCases))]
    public async Task TryRestoreAsync_Api_InvalidRequest_ReturnsExpectedStatus(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = InvalidApiSpecs[caseId];
        var (response, content) = await PostRawAsync(spec.Route);

        response.StatusCode.ShouldBe(spec.ExpectedStatus, content);
        if (!string.IsNullOrWhiteSpace(spec.MessageContains))
            content.ShouldContain(spec.MessageContains!);
    }

    private static IReadOnlyDictionary<string, ApiSuccessSpec> BuildApiSuccessSpecs()
        => new Dictionary<string, ApiSuccessSpec>
        {
            ["single"] = new(IsComposite: false),
            ["composite"] = new(IsComposite: true)
        };

    private static IReadOnlyDictionary<string, InvalidTryRestoreApiSpec> BuildInvalidApiSpecs()
        => new Dictionary<string, InvalidTryRestoreApiSpec>
        {
            ["single-invalid-guid"] = new(
                Route: "/api/product/not-a-guid/try-restore",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["single-notfound"] = new(
                Route: $"/api/product/{Guid.NewGuid()}/try-restore",
                ExpectedStatus: HttpStatusCode.NotFound),

            ["single-composite-route"] = new(
                Route: $"/api/product/try-restore/by-id?keys={Guid.NewGuid()}&keys={Guid.NewGuid()}",
                ExpectedStatus: HttpStatusCode.BadRequest,
                MessageContains: "Restore not supported for this entity."),

            ["composite-single-route"] = new(
                Route: $"/api/review/{Guid.NewGuid()}/try-restore",
                ExpectedStatus: HttpStatusCode.BadRequest,
                MessageContains: "Composite-key entities must use /try-restore/by-id with keys."),

            ["composite-notfound"] = new(
                Route: $"/api/review/try-restore/by-id?keys={Guid.NewGuid()}&keys={Guid.NewGuid()}",
                ExpectedStatus: HttpStatusCode.NotFound),

            ["composite-missing-keys"] = new(
                Route: "/api/review/try-restore/by-id",
                ExpectedStatus: HttpStatusCode.InternalServerError,
                MessageContains: "Key(s) are required."),

            ["composite-invalid-key-count"] = new(
                Route: $"/api/review/try-restore/by-id?keys={Guid.NewGuid()}",
                ExpectedStatus: HttpStatusCode.InternalServerError)
        };
}
