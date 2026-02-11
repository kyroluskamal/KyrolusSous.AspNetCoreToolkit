namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    private sealed record QueryHelperSpec(FilterClause Filter, Action<IReadOnlyList<Product>> Assert);

    private static readonly IReadOnlyDictionary<string, QueryHelperSpec> QueryHelperSpecs = BuildQueryHelperSpecs();

    public static TheoryData<string> QueryHelperCases => CaseIdsFrom(QueryHelperSpecs);

    [Theory(DisplayName = "GetAllIncludingDeletedAsync supports QueryHelper filters")]
    [MemberData(nameof(QueryHelperCases))]
    public async Task GetAllIncludingDeletedAsync_QueryHelper_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = QueryHelperSpecs[caseId];
        var request = new QueryRequest(Filters: [spec.Filter], IncludeDeleted: true);
        var (response, items, _) = await ArrangeAndActUseingHttpForListAsync<Product>(request);
        response.EnsureSuccessStatusCode();
        items.ShouldNotBeNull();
        spec.Assert(items);
    }

    private static IReadOnlyDictionary<string, QueryHelperSpec> BuildQueryHelperSpecs()
        => new Dictionary<string, QueryHelperSpec>
        {
            ["in"] = new QueryHelperSpec(
                new FilterClause("StockQuantity", "in", "25,50"),
                items => items.Select(p => p.StockQuantity).OrderBy(x => x).ShouldBe([25, 50])),

            ["between"] = new QueryHelperSpec(
                new FilterClause("Price", "between", "100,300"),
                items =>
                {
                    items.Count.ShouldBe(1);
                    items[0].Price.ShouldBe(199m);
                }),

            ["any"] = new QueryHelperSpec(
                new FilterClause("ProductCategories", "any", $"CategoryId = {DataSeeder.categoryElectronicsId}"),
                items => items.Count.ShouldBe(2)),

            ["all"] = new QueryHelperSpec(
                new FilterClause("ProductCategories", "all", $"CategoryId = {DataSeeder.categoryBooksId}"),
                items =>
                {
                    items.Count.ShouldBe(1);
                    items[0].Name.ShouldBe("Clean Code");
                }),

            ["notnull"] = new QueryHelperSpec(
                new FilterClause("Store", "notnull", null),
                items => items.Count.ShouldBe(3)),

            ["isnull"] = new QueryHelperSpec(
                new FilterClause("Store", "isnull", null),
                items => items.Count.ShouldBe(0))
        };

    // CaseIdsFrom is defined in GetAllIncludingDeletedAsyncTests.Helpers.cs
}
