namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.RestoreAsyncTests;

public partial class RestoreAsyncTests
{
    private sealed record InvalidRestoreApiSpec(
        string Route,
        HttpStatusCode ExpectedStatus,
        string? MessageContains = null);

    private static readonly IReadOnlyDictionary<string, InvalidRestoreApiSpec> InvalidApiSpecs = BuildInvalidApiSpecs();
    public static TheoryData<string> InvalidApiCases => CaseIdsFrom(InvalidApiSpecs);

    [Theory(DisplayName = "RestoreAsync API rejects invalid requests")]
    [MemberData(nameof(InvalidApiCases))]
    public async Task RestoreAsync_Api_InvalidRequest_ReturnsExpectedStatus(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = InvalidApiSpecs[caseId];
        var (response, content) = await PostRawAsync(spec.Route);

        response.StatusCode.ShouldBe(spec.ExpectedStatus, content);
        if (!string.IsNullOrWhiteSpace(spec.MessageContains))
            content.ShouldContain(spec.MessageContains!);
    }

    private static IReadOnlyDictionary<string, InvalidRestoreApiSpec> BuildInvalidApiSpecs()
        => new Dictionary<string, InvalidRestoreApiSpec>
        {
            ["single-invalid-guid"] = new(
                Route: "/api/product/not-a-guid/restore",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["single-notfound"] = new(
                Route: $"/api/product/{Guid.NewGuid()}/restore",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["single-composite-route"] = new(
                Route: $"/api/product/restore/by-id?keys={Guid.NewGuid()}&keys={Guid.NewGuid()}",
                ExpectedStatus: HttpStatusCode.BadRequest,
                MessageContains: "Restore not supported for this entity."),

            ["composite-single-route"] = new(
                Route: $"/api/review/{Guid.NewGuid()}/restore",
                ExpectedStatus: HttpStatusCode.BadRequest,
                MessageContains: "Composite-key entities must use /restore/by-id with keys."),

            ["composite-notfound"] = new(
                Route: $"/api/review/restore/by-id?keys={Guid.NewGuid()}&keys={Guid.NewGuid()}",
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["composite-missing-keys"] = new(
                Route: "/api/review/restore/by-id",
                ExpectedStatus: HttpStatusCode.InternalServerError,
                MessageContains: "Key(s) are required."),

            ["composite-invalid-key-count"] = new(
                Route: $"/api/review/restore/by-id?keys={Guid.NewGuid()}",
                ExpectedStatus: HttpStatusCode.InternalServerError)
        };
}
