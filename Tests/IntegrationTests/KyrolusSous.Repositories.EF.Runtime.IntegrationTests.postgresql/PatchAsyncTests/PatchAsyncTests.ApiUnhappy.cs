namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.PatchAsyncTests;

public partial class PatchAsyncTests
{
    private sealed record InvalidPatchApiSpec(
        string Route,
        string Payload,
        HttpStatusCode ExpectedStatus,
        string? MessageContains = null);

    private static readonly IReadOnlyDictionary<string, InvalidPatchApiSpec> InvalidApiSpecs = BuildInvalidApiSpecs();
    public static TheoryData<string> InvalidApiCases => CaseIdsFrom(InvalidApiSpecs);

    [Theory(DisplayName = "Patch API rejects invalid requests")]
    [MemberData(nameof(InvalidApiCases))]
    public async Task PatchAsync_Api_InvalidRequest_ReturnsExpectedStatus(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = InvalidApiSpecs[caseId];
        var (response, content) = await PatchRawAsync(spec.Route, spec.Payload);

        response.StatusCode.ShouldBe(spec.ExpectedStatus);
        if (!string.IsNullOrWhiteSpace(spec.MessageContains))
            content.ShouldContain(spec.MessageContains!);
    }

    private static IReadOnlyDictionary<string, InvalidPatchApiSpec> BuildInvalidApiSpecs()
        => new Dictionary<string, InvalidPatchApiSpec>
        {
            ["single-invalid-guid"] = new(
                Route: "/api/product/not-a-guid",
                Payload: "{}",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["single-notfound"] = new(
                Route: $"/api/product/{Guid.NewGuid()}",
                Payload: "{\"Name\":\"x\"}",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["single-invalid-property"] = new(
                Route: $"/api/product/{DataSeeder.productLaptopId}",
                Payload: "{\"NoSuchProperty\":\"x\"}",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["single-typed-update-fails"] = new(
                Route: $"/api/product/{DataSeeder.productLaptopId}",
                Payload: "{\"Name\":\"After\"}",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["single-null-body"] = new(
                Route: $"/api/product/{DataSeeder.productLaptopId}",
                Payload: "null",
                ExpectedStatus: HttpStatusCode.BadRequest),

            ["single-malformed-json"] = new(
                Route: $"/api/product/{DataSeeder.productLaptopId}",
                Payload: "{ invalid json }",
                ExpectedStatus: HttpStatusCode.BadRequest),

            ["composite-single-route"] = new(
                Route: $"/api/review/{Guid.NewGuid()}",
                Payload: "{}",
                ExpectedStatus: HttpStatusCode.BadRequest,
                MessageContains: "Composite-key entities must use /patch/by-id with keys."),

            ["composite-missing-keys"] = new(
                Route: "/api/review/by-id",
                Payload: "{\"Comment\":\"x\"}",
                ExpectedStatus: HttpStatusCode.InternalServerError,
                MessageContains: "Key(s) are required."),

            ["composite-notfound"] = new(
                Route: $"/api/review/by-id?keys={Guid.NewGuid()}&keys={Guid.NewGuid()}",
                Payload: "{\"Comment\":\"x\"}",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["composite-empty-updates"] = new(
                Route: $"/api/review/by-id?keys={DataSeeder.productLaptopId}&keys={DataSeeder.customerJaneId}",
                Payload: "{}",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["composite-invalid-property"] = new(
                Route: $"/api/review/by-id?keys={DataSeeder.productLaptopId}&keys={DataSeeder.customerJaneId}",
                Payload: "{\"NoSuchProperty\":\"x\"}",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["composite-null-body"] = new(
                Route: $"/api/review/by-id?keys={DataSeeder.productLaptopId}&keys={DataSeeder.customerJaneId}",
                Payload: "null",
                ExpectedStatus: HttpStatusCode.BadRequest),

            ["composite-malformed-json"] = new(
                Route: $"/api/review/by-id?keys={DataSeeder.productLaptopId}&keys={DataSeeder.customerJaneId}",
                Payload: "{ invalid json }",
                ExpectedStatus: HttpStatusCode.BadRequest)
        };
}
