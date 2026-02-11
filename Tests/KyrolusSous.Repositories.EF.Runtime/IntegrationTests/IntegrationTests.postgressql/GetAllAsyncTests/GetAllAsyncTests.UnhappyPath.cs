namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    private sealed record InvalidQuerySpec(QueryRequest Request, HttpStatusCode ExpectedStatus, string MessageContains);

    private static readonly IReadOnlyDictionary<string, InvalidQuerySpec> InvalidQuerySpecs = BuildInvalidQuerySpecs();

    public static TheoryData<string> InvalidQueryCases => CaseIdsFrom(InvalidQuerySpecs);

    [Fact(DisplayName = "GetAllAsync throws when include string is invalid navigation")]
    public async Task GetAllAsync_InvalidIncludeString_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await repo.GetAllAsync(
                filter: null,
                orderBy: null,
                includeProperties: ["NotARealNavigation"],
                includeGraph: null,
                asNoTracking: true,
                useSplitQuery: true,
                cancellationToken: default);
        });
    }

    [Theory(DisplayName = "GetAllAsync rejects invalid query requests")]
    [MemberData(nameof(InvalidQueryCases))]
    public async Task GetAllAsync_InvalidQuery_Throws(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = InvalidQuerySpecs[caseId];
        var (response, content) = await GetErrorAsync<Product>(spec.Request);
        response.StatusCode.ShouldBe(spec.ExpectedStatus);
        content?.ShouldContain(spec.MessageContains);
    }

    [Fact(DisplayName = "GetAllAsync should not throw when QueryRequest is null")]
    public async Task GetAllAsync_NullQueryRequest_Not_Throws()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/product?request=null");
        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        var items = JsonSerializer.Deserialize<List<Product>>(content, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        items.ShouldNotBeNull();
        items.Count.ShouldBe(3);
    }

    private static IReadOnlyDictionary<string, InvalidQuerySpec> BuildInvalidQuerySpecs()
        => new Dictionary<string, InvalidQuerySpec>
        {
            ["unsupported-string-op"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Name", "has", "Test")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter: property='Name', operator='has', value='Test'"),

            ["unsupported-numeric-op"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("StockQuantity", "has", "25")]),
                HttpStatusCode.InternalServerError,
                "Unsupported operator 'has'"),

            ["unsupported-bool-op"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("IsActive", "gt", "true")]),
                HttpStatusCode.InternalServerError,
                "Unsupported operator 'gt'"),

            ["unsupported-datetimeoffset-op"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("CreatedAt", "contains", "2024-06-01T00:00:00Z")]),
                HttpStatusCode.InternalServerError,
                "Unsupported operator 'contains'"),

            ["unsupported-numeric-contains"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("StockQuantity", "contains", "25")]),
                HttpStatusCode.InternalServerError,
                "Unsupported operator 'contains'"),

            ["unsupported-guid-op"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Id", "gt", "66666666-6666-6666-6666-666666666661")]),
                HttpStatusCode.InternalServerError,
                "Unsupported operator 'gt'"),

            ["invalid-filter-property"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("NotARealProperty", "eq", "SomeValue")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter: property='NotARealProperty', operator='eq', value='SomeValue'"),

            ["empty-filter-property"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("", "eq", "SomeValue")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter: 'Property' is required"),

            ["null-filter-property"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause(null!, "eq", "SomeValue")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter: 'Property' is required"),

            ["empty-operator"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Name", "", "SomeValue")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter for property 'Name': 'Operator' is required"),

            ["null-operator"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Name", null!, "SomeValue")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter for property 'Name': 'Operator' is required"),

            ["invalid-orderby-property"] = new InvalidQuerySpec(
                new QueryRequest(OrderBy: [new OrderClause("NotARealProperty")]),
                HttpStatusCode.InternalServerError,
                "Invalid orderBy: property='NotARealProperty' not found on entity 'Product'"),

            ["empty-orderby-property"] = new InvalidQuerySpec(
                new QueryRequest(OrderBy: [new OrderClause("")]),
                HttpStatusCode.InternalServerError,
                "Invalid orderBy: 'Property' is required"),

            ["null-orderby-property"] = new InvalidQuerySpec(
                new QueryRequest(OrderBy: [new OrderClause(null!)]),
                HttpStatusCode.InternalServerError,
                "Invalid orderBy: 'Property' is required"),

            ["invalid-numeric-value"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("StockQuantity", "eq", "NotANumber")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter: property='StockQuantity', operator='eq', value='NotANumber'"),

            ["invalid-guid-value"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Id", "eq", "NotAGuid")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter: property='Id', operator='eq', value='NotAGuid'"),

            ["invalid-datetimeoffset-value"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("CreatedAt", "eq", "NotADateTimeOffset")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter: property='CreatedAt', operator='eq', value='NotADateTimeOffset'"),

            ["invalid-bool-value"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("IsActive", "eq", "NotABool")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter: property='IsActive', operator='eq', value='NotABool'"),

            ["one-valid-one-invalid-filter"] = new InvalidQuerySpec(
                new QueryRequest(Filters:
                [
                    new FilterClause("Name", "contains", "Code"),
                    new FilterClause("StockQuantity", "gt", "NotANumber")
                ]),
                HttpStatusCode.InternalServerError,
                "Invalid filter: property='StockQuantity', operator='gt', value='NotANumber'"),

            ["one-valid-one-invalid-orderby"] = new InvalidQuerySpec(
                new QueryRequest(OrderBy: [new OrderClause("Name"), new OrderClause("NotARealProperty")]),
                HttpStatusCode.InternalServerError,
                "Invalid orderBy: property='NotARealProperty' not found on entity 'Product'"),

            ["invalid-filter-and-orderby"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("NotARealProperty", "eq", "SomeValue")], OrderBy: [new OrderClause("AlsoNotARealProperty")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter: property='NotARealProperty', operator='eq', value='SomeValue'"),

            ["invalid-include-string"] = new InvalidQuerySpec(
                new QueryRequest(Includes: ["Review", "NotARealNavigation"]),
                HttpStatusCode.InternalServerError,
                "InvalidInclude"),

            ["invalid-between"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Name", "between", "A..Z")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter: property='Name', operator='between', value='A..Z'")
        };

    // CaseIdsFrom is defined in GetAllAsyncTests.Helpers.cs
}
