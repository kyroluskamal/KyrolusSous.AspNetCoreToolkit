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
                "Invalid filter: property='Name', operator='between', value='A..Z'"),

            ["between-malformed-quote-start"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Price", "between", "\"100..300")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["between-malformed-quote-end"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Price", "between", "100..\"300")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["between-malformed-escape"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Price", "between", "\"100\\\"..\"300\"")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["string-relational-gt"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Name", "gt", "Alpha")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter: property='Name', operator='gt', value='Alpha'"),

            ["guid-contains"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Id", "contains", "66666666-6666-6666-6666-666666666661")]),
                HttpStatusCode.InternalServerError,
                "Unsupported operator 'contains'"),

            ["bool-between"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("IsActive", "between", "true,false")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["timespan-relational-gt"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("FinishedAt", "gt", "1.00:00:00")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["in-stockquantity-invalid"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("StockQuantity", "in", "NotANumber")]),
                HttpStatusCode.InternalServerError,
                "could not be converted"),

            ["between-stockquantity-invalid"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("StockQuantity", "between", "NotANumber..20")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["between-guid-invalid"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Id", "between", "66666666-6666-6666-6666-666666666661..66666666-6666-6666-6666-666666666662")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["in-null-nonnullable"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("StockQuantity", "in", "null,25")]),
                HttpStatusCode.InternalServerError,
                "does not support NULL"),

            ["any-noncollection"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Name", "any", "A")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["all-noncollection"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Name", "all", "A")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["nested-rating-isnull"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Reviews", "any", "Rating isnull")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["nested-rating-null-relational"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Reviews", "any", "Rating > null")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["nested-rating-invalid-conversion"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Reviews", "any", "Rating==NotANumber")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["nested-rating-in-invalid"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Reviews", "any", "Rating in [5,bad]")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["nested-rating-in-empty-token"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Reviews", "any", "Rating in [5,,4]")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["nested-empty-property-segments"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Reviews", "any", ".==1")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["nested-contains-on-numeric"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Reviews", "any", "Rating contains 5")]),
                HttpStatusCode.InternalServerError,
                "TargetInvocationException"),

            ["nested-missing-closing-parenthesis"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Reviews", "any", "(Rating == 5")]),
                HttpStatusCode.InternalServerError,
                "Exception"),

            ["nested-missing-closing-quote"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Reviews", "any", "Comment == \"Good sound")]),
                HttpStatusCode.InternalServerError,
                "Exception"),

            ["nested-value-required"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Reviews", "any", "Rating ==")]),
                HttpStatusCode.InternalServerError,
                "Exception"),

            ["nested-operator-required"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Reviews", "any", "Rating")]),
                HttpStatusCode.InternalServerError,
                "Exception"),

            ["nested-property-required"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Reviews", "any", "== 5")]),
                HttpStatusCode.InternalServerError,
                "Exception"),

            ["nested-missing-closing-bracket"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Reviews", "any", "Rating in [4,5")]),
                HttpStatusCode.InternalServerError,
                "Exception"),

            ["nested-between-missing-end"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Reviews", "any", "AddedIn between 2024-06-01..")]),
                HttpStatusCode.InternalServerError,
                "Exception"),

            ["nested-between-timespan-unsupported"] = new InvalidQuerySpec(
                new QueryRequest(Filters: [new FilterClause("Reviews", "any", "FinishedAt between 1.00:00:00..1.00:00:00")]),
                HttpStatusCode.InternalServerError,
                "Exception")
        };

    // CaseIdsFrom is defined in GetAllAsyncTests.Helpers.cs
}
