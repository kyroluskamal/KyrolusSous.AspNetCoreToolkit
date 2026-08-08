namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.RemoveAsyncTests;

public partial class RemoveAsyncTests
{
    private sealed record InvalidRemoveApiSpec(
        string Route,
        string? Payload,
        HttpStatusCode ExpectedStatus,
        string? MessageContains = null);

    private static readonly IReadOnlyDictionary<string, InvalidRemoveApiSpec> InvalidApiSpecs = BuildInvalidApiSpecs();

    public static TheoryData<string> InvalidApiCases => CaseIdsFrom(InvalidApiSpecs);

    [Theory(DisplayName = "Remove API rejects invalid requests")]
    [MemberData(nameof(InvalidApiCases))]
    public async Task RemoveAsync_Api_InvalidRequest_ReturnsExpectedStatus(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = InvalidApiSpecs[caseId];
        var (response, content) = await DeleteRawAsync(spec.Route, spec.Payload);

        response.StatusCode.ShouldBe(spec.ExpectedStatus);
        if (!string.IsNullOrWhiteSpace(spec.MessageContains))
            content.ShouldContain(spec.MessageContains);
    }

    private static IReadOnlyDictionary<string, InvalidRemoveApiSpec> BuildInvalidApiSpecs()
        => new Dictionary<string, InvalidRemoveApiSpec>
        {
            ["single-invalid-guid"] = new(
                Route: "/api/product/not-a-guid?softDelete=false",
                Payload: null,
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["single-notfound"] = new(
                Route: $"/api/product/{Guid.NewGuid()}?softDelete=false",
                Payload: null,
                ExpectedStatus: HttpStatusCode.NotFound),

            ["single-composite-route"] = new(
                Route: $"/api/product/by-id?keys={Guid.NewGuid()}&softDelete=false",
                Payload: null,
                ExpectedStatus: HttpStatusCode.BadRequest,
                MessageContains: "Composite-key endpoint requires composite-key repo."),

            ["composite-single-route"] = new(
                Route: $"/api/review/{Guid.NewGuid()}?softDelete=false",
                Payload: null,
                ExpectedStatus: HttpStatusCode.BadRequest,
                MessageContains: "Composite-key entities must use /by-id with keys."),

            ["composite-notfound"] = new(
                Route: $"/api/review/by-id?keys={Guid.NewGuid()}&keys={Guid.NewGuid()}&softDelete=false",
                Payload: null,
                ExpectedStatus: HttpStatusCode.NotFound),

            ["composite-missing-keys"] = new(
                Route: "/api/review/by-id?softDelete=false",
                Payload: null,
                ExpectedStatus: HttpStatusCode.InternalServerError,
                MessageContains: "Key(s) are required."),

            ["remove-range-null-body"] = new(
                Route: "/api/product/remove-range?softDelete=false",
                Payload: "null",
                ExpectedStatus: HttpStatusCode.BadRequest),

            ["remove-range-malformed-json"] = new(
                Route: "/api/product/remove-range?softDelete=false",
                Payload: "{ invalid json }",
                ExpectedStatus: HttpStatusCode.BadRequest),

            ["remove-range-composite-null-body"] = new(
                Route: "/api/review/remove-range?softDelete=false",
                Payload: "null",
                ExpectedStatus: HttpStatusCode.BadRequest)
        };
}
