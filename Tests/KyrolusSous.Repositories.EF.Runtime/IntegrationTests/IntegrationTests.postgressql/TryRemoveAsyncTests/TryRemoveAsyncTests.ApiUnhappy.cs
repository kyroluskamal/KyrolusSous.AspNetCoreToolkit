namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryRemoveAsyncTests;

public partial class TryRemoveAsyncTests
{
    private sealed record InvalidTryRemoveApiSpec(
        string Route,
        HttpStatusCode ExpectedStatus,
        string? MessageContains = null);

    private static readonly IReadOnlyDictionary<string, InvalidTryRemoveApiSpec> InvalidApiSpecs = BuildInvalidApiSpecs();
    public static TheoryData<string> InvalidApiCases => CaseIdsFrom(InvalidApiSpecs);

    [Theory(DisplayName = "TryRemoveAsync API rejects invalid requests")]
    [MemberData(nameof(InvalidApiCases))]
    public async Task TryRemoveAsync_Api_InvalidRequest_ReturnsExpectedStatus(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = InvalidApiSpecs[caseId];
        var (response, content) = await DeleteRawAsync(spec.Route);

        response.StatusCode.ShouldBe(spec.ExpectedStatus, content);
        if (!string.IsNullOrWhiteSpace(spec.MessageContains))
            content.ShouldContain(spec.MessageContains!);
    }

    private static IReadOnlyDictionary<string, InvalidTryRemoveApiSpec> BuildInvalidApiSpecs()
        => new Dictionary<string, InvalidTryRemoveApiSpec>
        {
            ["single-invalid-guid"] = new(
                Route: "/api/product/not-a-guid/try?softDelete=false",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["single-notfound"] = new(
                Route: $"/api/product/{Guid.NewGuid()}/try?softDelete=false",
                ExpectedStatus: HttpStatusCode.NotFound),

            ["single-composite-route"] = new(
                Route: $"/api/product/try/by-id?keys={Guid.NewGuid()}&softDelete=false",
                ExpectedStatus: HttpStatusCode.BadRequest,
                MessageContains: "Composite-key endpoint requires composite-key repo."),

            ["composite-single-route"] = new(
                Route: $"/api/review/{Guid.NewGuid()}/try?softDelete=false",
                ExpectedStatus: HttpStatusCode.BadRequest,
                MessageContains: "Composite-key entities must use /try/by-id with keys."),

            ["composite-notfound"] = new(
                Route: $"/api/review/try/by-id?keys={Guid.NewGuid()}&keys={Guid.NewGuid()}&softDelete=false",
                ExpectedStatus: HttpStatusCode.NotFound),

            ["composite-missing-keys"] = new(
                Route: "/api/review/try/by-id?softDelete=false",
                ExpectedStatus: HttpStatusCode.InternalServerError,
                MessageContains: "Key(s) are required."),

            ["composite-invalid-key-count"] = new(
                Route: $"/api/review/try/by-id?keys={Guid.NewGuid()}&softDelete=false",
                ExpectedStatus: HttpStatusCode.InternalServerError)
        };
}
