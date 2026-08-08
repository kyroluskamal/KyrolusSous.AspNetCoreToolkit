namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.ExecuteUpdateAsyncTests;

public partial class ExecuteUpdateAsyncTests
{
    private sealed record InvalidApiSpec(string Route, string Payload, HttpStatusCode ExpectedStatus, string? MessageContains = null);

    private static readonly IReadOnlyDictionary<string, InvalidApiSpec> InvalidApiSpecs = BuildInvalidApiSpecs();
    public static TheoryData<string> InvalidApiCases => CaseIdsFrom(InvalidApiSpecs);

    [Theory(DisplayName = "ExecuteUpdate API rejects invalid requests")]
    [MemberData(nameof(InvalidApiCases))]
    public async Task ExecuteUpdateAsync_Api_InvalidRequest_ReturnsExpectedStatus(string caseId)
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
            ["product-empty-updates"] = new(
                Route: "/api/product/execute-update",
                Payload: JsonSerializer.Serialize(new
                {
                    request = new QueryRequest(Filters:
                    [
                        new FilterClause("Id", "eq", DataSeeder.productLaptopId.ToString())
                    ]),
                    updates = Array.Empty<object>()
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.BadRequest,
                MessageContains: "At least one property update is required."),

            ["product-null-updates"] = new(
                Route: "/api/product/execute-update",
                Payload: JsonSerializer.Serialize(new
                {
                    request = new QueryRequest(),
                    updates = (object?)null
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.BadRequest,
                MessageContains: "At least one property update is required."),

            ["product-whitespace-property"] = new(
                Route: "/api/product/execute-update",
                Payload: JsonSerializer.Serialize(new
                {
                    request = new QueryRequest(Filters:
                    [
                        new FilterClause("Id", "eq", DataSeeder.productLaptopId.ToString())
                    ]),
                    updates = new[]
                    {
                        new { property = " ", value = "x" }
                    }
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError,
                MessageContains: "Property name is required."),

            ["product-property-not-found"] = new(
                Route: "/api/product/execute-update",
                Payload: JsonSerializer.Serialize(new
                {
                    request = new QueryRequest(Filters:
                    [
                        new FilterClause("Id", "eq", DataSeeder.productLaptopId.ToString())
                    ]),
                    updates = new[]
                    {
                        new { property = "NoSuchProperty", value = "x" }
                    }
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError,
                MessageContains: "Property 'NoSuchProperty' not found."),

            ["product-value-conversion-fails"] = new(
                Route: "/api/product/execute-update",
                Payload: JsonSerializer.Serialize(new
                {
                    request = new QueryRequest(Filters:
                    [
                        new FilterClause("Id", "eq", DataSeeder.productLaptopId.ToString())
                    ]),
                    updates = new[]
                    {
                        new { property = "StockQuantity", value = "not-an-int" }
                    }
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError),

            ["review-property-not-found"] = new(
                Route: "/api/review/execute-update",
                Payload: JsonSerializer.Serialize(new
                {
                    request = new QueryRequest(Filters:
                    [
                        new FilterClause("ProductId", "eq", DataSeeder.productLaptopId.ToString()),
                        new FilterClause("CustomerId", "eq", DataSeeder.customerJaneId.ToString())
                    ]),
                    updates = new[]
                    {
                        new { property = "NoSuchProperty", value = "x" }
                    }
                }, JsonOptions),
                ExpectedStatus: HttpStatusCode.InternalServerError,
                MessageContains: "Property 'NoSuchProperty' not found."),

            ["malformed-json"] = new(
                Route: "/api/product/execute-update",
                Payload: "{ invalid json }",
                ExpectedStatus: HttpStatusCode.BadRequest),

            ["null-body"] = new(
                Route: "/api/product/execute-update",
                Payload: "null",
                ExpectedStatus: HttpStatusCode.BadRequest)
        };
}
