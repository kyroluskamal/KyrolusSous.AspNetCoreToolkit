namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.UpdateRangeAsyncTests;

public partial class UpdateRangeAsyncTests
{
    private sealed record InvalidUpdateRangeApiSpec(
        string Route,
        Func<string> BuildPayload,
        HttpStatusCode ExpectedStatus,
        string? MessageContains = null);

    private static readonly IReadOnlyDictionary<string, InvalidUpdateRangeApiSpec> InvalidApiSpecs = BuildInvalidApiSpecs();
    public static TheoryData<string> InvalidApiCases => CaseIdsFrom(InvalidApiSpecs);

    [Theory(DisplayName = "UpdateRangeAsync API rejects invalid payloads")]
    [MemberData(nameof(InvalidApiCases))]
    public async Task UpdateRangeAsync_Api_InvalidPayload_ReturnsExpectedStatus(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = InvalidApiSpecs[caseId];
        var (response, content) = await PutRawAsync($"/api/{spec.Route}/update-range", spec.BuildPayload());

        response.StatusCode.ShouldBe(spec.ExpectedStatus);
        if (!string.IsNullOrWhiteSpace(spec.MessageContains))
            content.ShouldContain(spec.MessageContains!);
    }

    private static IReadOnlyDictionary<string, InvalidUpdateRangeApiSpec> BuildInvalidApiSpecs()
        => new Dictionary<string, InvalidUpdateRangeApiSpec>
        {
            ["product-empty-array"] = new(
                Route: "product",
                BuildPayload: static () => "[]",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["product-notfound"] = new(
                Route: "product",
                BuildPayload: () => JsonSerializer.Serialize(new List<Product>
                {
                    CreateValidProduct(id: Guid.NewGuid(), name: "missing")
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["product-duplicate-sku"] = new(
                Route: "product",
                BuildPayload: () => JsonSerializer.Serialize(new List<Product>
                {
                    CreateValidProduct(id: DataSeeder.productLaptopId, sku: "LP-15", name: "Laptop"),
                    CreateValidProduct(id: DataSeeder.productHeadphonesId, sku: "LP-15", name: "Headphones")
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["product-invalid-store"] = new(
                Route: "product",
                BuildPayload: () => JsonSerializer.Serialize(new List<Product>
                {
                    CreateValidProduct(id: DataSeeder.productLaptopId, storeId: Guid.NewGuid(), name: "Invalid store")
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["review-empty-array"] = new(
                Route: "review",
                BuildPayload: static () => "[]",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["review-notfound"] = new(
                Route: "review",
                BuildPayload: () => JsonSerializer.Serialize(new List<Review>
                {
                    CreateValidReview(Guid.NewGuid(), Guid.NewGuid(), rating: 3, comment: "missing")
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["review-mixed-existing-and-missing"] = new(
                Route: "review",
                BuildPayload: () => JsonSerializer.Serialize(new List<Review>
                {
                    CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJaneId, rating: 4, comment: "exists"),
                    CreateValidReview(Guid.NewGuid(), DataSeeder.customerJohnId, rating: 5, comment: "missing")
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["null-body"] = new(
                Route: "product",
                BuildPayload: static () => "null",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["malformed-json"] = new(
                Route: "product",
                BuildPayload: static () => "[{ invalid json payload }]",
                ExpectedStatus: HttpStatusCode.InternalServerError)
        };
}
