namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryPatchAsyncTests;

public partial class TryPatchAsyncTests
{
    private sealed record InvalidTryPatchApiSpec(
        string Route,
        string Payload,
        HttpStatusCode ExpectedStatus,
        string? MessageContains = null);

    private static readonly IReadOnlyDictionary<string, InvalidTryPatchApiSpec> InvalidApiSpecs = BuildInvalidApiSpecs();
    public static TheoryData<string> InvalidApiCases => CaseIdsFrom(InvalidApiSpecs);

    [Theory(DisplayName = "TryPatchAsync API rejects invalid requests")]
    [MemberData(nameof(InvalidApiCases))]
    public async Task TryPatchAsync_Api_InvalidRequest_ReturnsExpectedStatus(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = InvalidApiSpecs[caseId];
        var (response, content) = await PatchRawAsync(spec.Route, spec.Payload);

        response.StatusCode.ShouldBe(spec.ExpectedStatus);
        if (!string.IsNullOrWhiteSpace(spec.MessageContains))
            content.ShouldContain(spec.MessageContains!);
    }

    private static IReadOnlyDictionary<string, InvalidTryPatchApiSpec> BuildInvalidApiSpecs()
        => new Dictionary<string, InvalidTryPatchApiSpec>
        {
            ["single-invalid-guid"] = new(
                Route: "/api/product/not-a-guid/try",
                Payload: "{}",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["single-notfound"] = new(
                Route: $"/api/product/{Guid.NewGuid()}/try",
                Payload: "{\"Name\":\"x\"}",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["single-empty-updates"] = new(
                Route: $"/api/product/{DataSeeder.productLaptopId}/try",
                Payload: "{}",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["single-invalid-property"] = new(
                Route: $"/api/product/{DataSeeder.productLaptopId}/try",
                Payload: "{\"NoSuchProperty\":\"x\"}",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["single-null-body"] = new(
                Route: $"/api/product/{DataSeeder.productLaptopId}/try",
                Payload: "null",
                ExpectedStatus: HttpStatusCode.BadRequest),

            ["single-malformed-json"] = new(
                Route: $"/api/product/{DataSeeder.productLaptopId}/try",
                Payload: "{ invalid json }",
                ExpectedStatus: HttpStatusCode.BadRequest),

            ["single-composite-route"] = new(
                Route: $"/api/product/try/by-id?keys={DataSeeder.productLaptopId}&keys={DataSeeder.customerJaneId}",
                Payload: "{}",
                ExpectedStatus: HttpStatusCode.BadRequest,
                MessageContains: "Composite-key endpoint requires composite-key repo."),

            ["composite-single-route"] = new(
                Route: $"/api/review/{Guid.NewGuid()}/try",
                Payload: "{}",
                ExpectedStatus: HttpStatusCode.BadRequest,
                MessageContains: "Composite-key entities must use /patch/by-id with keys."),

            ["composite-missing-keys"] = new(
                Route: "/api/review/try/by-id",
                Payload: "{\"Comment\":\"x\"}",
                ExpectedStatus: HttpStatusCode.InternalServerError,
                MessageContains: "Key(s) are required."),

            ["composite-notfound"] = new(
                Route: $"/api/review/try/by-id?keys={Guid.NewGuid()}&keys={Guid.NewGuid()}",
                Payload: "{\"Comment\":\"x\"}",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["composite-empty-updates"] = new(
                Route: $"/api/review/try/by-id?keys={DataSeeder.productLaptopId}&keys={DataSeeder.customerJaneId}",
                Payload: "{}",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["composite-invalid-property"] = new(
                Route: $"/api/review/try/by-id?keys={DataSeeder.productLaptopId}&keys={DataSeeder.customerJaneId}",
                Payload: "{\"NoSuchProperty\":\"x\"}",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["composite-null-body"] = new(
                Route: $"/api/review/try/by-id?keys={DataSeeder.productLaptopId}&keys={DataSeeder.customerJaneId}",
                Payload: "null",
                ExpectedStatus: HttpStatusCode.BadRequest),

            ["composite-malformed-json"] = new(
                Route: $"/api/review/try/by-id?keys={DataSeeder.productLaptopId}&keys={DataSeeder.customerJaneId}",
                Payload: "{ invalid json }",
                ExpectedStatus: HttpStatusCode.BadRequest)
        };
}
