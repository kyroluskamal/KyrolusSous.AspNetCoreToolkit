namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.ExecuteDeleteAsyncTests;

public partial class ExecuteDeleteAsyncTests
{
    private sealed record InvalidApiSpec(string Route, string Payload, HttpStatusCode ExpectedStatus, string? MessageContains = null);

    private static readonly IReadOnlyDictionary<string, InvalidApiSpec> InvalidApiSpecs = BuildInvalidApiSpecs();
    public static TheoryData<string> InvalidApiCases => CaseIdsFrom(InvalidApiSpecs);

    [Theory(DisplayName = "ExecuteDelete API rejects invalid requests")]
    [MemberData(nameof(InvalidApiCases))]
    public async Task ExecuteDeleteAsync_Api_InvalidRequest_ReturnsExpectedStatus(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = InvalidApiSpecs[caseId];

        var (response, content) = await PostRawAsync(spec.Route, spec.Payload);
        response.StatusCode.ShouldBe(spec.ExpectedStatus);
        if (!string.IsNullOrWhiteSpace(spec.MessageContains))
            content.ShouldContain(spec.MessageContains!);
    }

    private static IReadOnlyDictionary<string, InvalidApiSpec> BuildInvalidApiSpecs()
        => new Dictionary<string, InvalidApiSpec>
        {
            ["product-malformed-json"] = new(
                Route: "/api/product/execute-delete",
                Payload: "{ invalid json }",
                ExpectedStatus: HttpStatusCode.BadRequest),

            ["product-null-body"] = new(
                Route: "/api/product/execute-delete",
                Payload: "null",
                ExpectedStatus: HttpStatusCode.BadRequest),

            ["product-invalid-filter-property"] = new(
                Route: "/api/product/execute-delete",
                Payload: JsonSerializer.Serialize(new QueryRequest(Filters:
                [
                    new FilterClause("NoSuchProperty", "eq", "x")
                ]), JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["product-invalid-operator"] = new(
                Route: "/api/product/execute-delete",
                Payload: JsonSerializer.Serialize(new QueryRequest(Filters:
                [
                    new FilterClause("Name", "not-an-operator", "x")
                ]), JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["review-invalid-filter-property"] = new(
                Route: "/api/review/execute-delete",
                Payload: JsonSerializer.Serialize(new QueryRequest(Filters:
                [
                    new FilterClause("NoSuchProperty", "eq", "x")
                ]), JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["review-invalid-operator"] = new(
                Route: "/api/review/execute-delete",
                Payload: JsonSerializer.Serialize(new QueryRequest(Filters:
                [
                    new FilterClause("Comment", "bad-op", "x")
                ]), JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError)
        };
}
