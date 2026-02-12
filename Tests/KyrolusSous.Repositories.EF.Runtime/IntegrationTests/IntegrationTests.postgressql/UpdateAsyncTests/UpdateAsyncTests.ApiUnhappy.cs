namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.UpdateAsyncTests;

public partial class UpdateAsyncTests
{
    private sealed record InvalidUpdateApiSpec(
        string Route,
        Func<string> BuildPayload,
        HttpStatusCode ExpectedStatus,
        string? MessageContains = null);

    private static readonly IReadOnlyDictionary<string, InvalidUpdateApiSpec> InvalidApiSpecs = BuildInvalidApiSpecs();
    public static TheoryData<string> InvalidApiCases => CaseIdsFrom(InvalidApiSpecs);

    [Theory(DisplayName = "UpdateAsync API rejects invalid payloads")]
    [MemberData(nameof(InvalidApiCases))]
    public async Task UpdateAsync_Api_InvalidPayload_ReturnsExpectedStatus(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = InvalidApiSpecs[caseId];
        var (response, content) = await PutRawAsync($"/api/{spec.Route}", spec.BuildPayload());

        response.StatusCode.ShouldBe(spec.ExpectedStatus);
        if (!string.IsNullOrWhiteSpace(spec.MessageContains))
            content.ShouldContain(spec.MessageContains!);
    }

    private static IReadOnlyDictionary<string, InvalidUpdateApiSpec> BuildInvalidApiSpecs()
        => new Dictionary<string, InvalidUpdateApiSpec>
        {
            ["product-notfound"] = new(
                Route: $"product/{Guid.NewGuid()}",
                BuildPayload: () => JsonSerializer.Serialize(CreateValidProduct(id: Guid.NewGuid(), name: "notfound"), JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["product-duplicate-sku"] = new(
                Route: $"product/{DataSeeder.productHeadphonesId}",
                BuildPayload: () => JsonSerializer.Serialize(CreateValidProduct(id: DataSeeder.productHeadphonesId, sku: "LP-15", name: "dup"), JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["product-invalid-store"] = new(
                Route: $"product/{DataSeeder.productHeadphonesId}",
                BuildPayload: () => JsonSerializer.Serialize(CreateValidProduct(id: DataSeeder.productHeadphonesId, storeId: Guid.NewGuid(), name: "invalid store"), JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["review-notfound"] = new(
                Route: "review/ignored",
                BuildPayload: () => JsonSerializer.Serialize(CreateValidReview(Guid.NewGuid(), Guid.NewGuid(), rating: 2), JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["null-body"] = new(
                Route: $"product/{DataSeeder.productHeadphonesId}",
                BuildPayload: static () => "null",
                ExpectedStatus: HttpStatusCode.BadRequest),

            ["malformed-json"] = new(
                Route: $"product/{DataSeeder.productHeadphonesId}",
                BuildPayload: static () => "{ invalid json }",
                ExpectedStatus: HttpStatusCode.BadRequest)
        };
}

