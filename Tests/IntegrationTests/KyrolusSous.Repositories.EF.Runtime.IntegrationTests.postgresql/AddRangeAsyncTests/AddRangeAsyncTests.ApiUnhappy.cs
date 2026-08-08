namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.AddRangeAsyncTests;

public partial class AddRangeAsyncTests
{
    private sealed record InvalidAddRangeApiSpec(
        string Route,
        Func<string> BuildPayload,
        HttpStatusCode ExpectedStatus,
        string? MessageContains = null);

    private static readonly IReadOnlyDictionary<string, InvalidAddRangeApiSpec> InvalidApiSpecs = BuildInvalidApiSpecs();
    public static TheoryData<string> InvalidApiCases => CaseIdsFrom(InvalidApiSpecs);

    [Theory(DisplayName = "AddRangeAsync API rejects invalid payloads")]
    [MemberData(nameof(InvalidApiCases))]
    public async Task AddRangeAsync_Api_InvalidPayload_ReturnsExpectedStatus(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = InvalidApiSpecs[caseId];
        var (response, content) = await PostRawAsync($"/api/{spec.Route}/add-range", spec.BuildPayload());

        response.StatusCode.ShouldBe(spec.ExpectedStatus);
        if (!string.IsNullOrWhiteSpace(spec.MessageContains))
            content.ShouldContain(spec.MessageContains!);
    }

    private static IReadOnlyDictionary<string, InvalidAddRangeApiSpec> BuildInvalidApiSpecs()
        => new Dictionary<string, InvalidAddRangeApiSpec>
        {
            ["product-empty-array"] = new(
                Route: "product",
                BuildPayload: static () => "[]",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["product-duplicate-id"] = new(
                Route: "product",
                BuildPayload: () => JsonSerializer.Serialize(new List<Product>
                {
                    CreateValidProduct(id: DataSeeder.productLaptopId, sku: $"DUP-{Guid.NewGuid():N}", name: "Duplicate Product"),
                    CreateValidProduct()
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["product-duplicate-unique-sku"] = new(
                Route: "product",
                BuildPayload: () => JsonSerializer.Serialize(new List<Product>
                {
                    CreateValidProduct(sku: "LP-15", name: "Duplicate SKU Product"),
                    CreateValidProduct()
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["product-invalid-store"] = new(
                Route: "product",
                BuildPayload: () => JsonSerializer.Serialize(new List<Product>
                {
                    CreateValidProduct(storeId: Guid.NewGuid(), name: "Invalid FK Product")
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["review-empty-array"] = new(
                Route: "review",
                BuildPayload: static () => "[]",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["review-duplicate-composite-key"] = new(
                Route: "review",
                BuildPayload: () => JsonSerializer.Serialize(new List<Review>
                {
                    CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJaneId, rating: 1),
                    CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2)
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["review-invalid-product"] = new(
                Route: "review",
                BuildPayload: () => JsonSerializer.Serialize(new List<Review>
                {
                    CreateValidReview(Guid.NewGuid(), DataSeeder.customerJohnId, rating: 3)
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["review-invalid-customer"] = new(
                Route: "review",
                BuildPayload: () => JsonSerializer.Serialize(new List<Review>
                {
                    CreateValidReview(DataSeeder.productLaptopId, Guid.NewGuid(), rating: 3)
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["null-body"] = new(
                Route: "product",
                BuildPayload: static () => "null",
                ExpectedStatus: HttpStatusCode.BadRequest),

            ["malformed-json"] = new(
                Route: "product",
                BuildPayload: static () => "[{ invalid json payload }]",
                ExpectedStatus: HttpStatusCode.BadRequest)
        };
}
