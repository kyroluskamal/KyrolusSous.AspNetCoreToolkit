namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    private sealed record FilterOrderingSpec(QueryRequest Request, Action<List<Product>> Assert);

    private static readonly IReadOnlyDictionary<string, FilterOrderingSpec> FilterOrderingSpecs = BuildFilterOrderingSpecs();

    public static TheoryData<string> FilterOrderingCases => CaseIdsFrom(FilterOrderingSpecs);

    [Theory(DisplayName = "GetAllAsync supports filtering and ordering combinations")]
    [MemberData(nameof(FilterOrderingCases))]
    public async Task GetAllAsync_FilteringOrdering_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = FilterOrderingSpecs[caseId];
        var products = await GetOkListAsync<Product>(spec.Request);
        spec.Assert(products);
    }

    private static IReadOnlyDictionary<string, FilterOrderingSpec> BuildFilterOrderingSpecs()
        => new Dictionary<string, FilterOrderingSpec>
        {
            ["ordering-asc"] = new FilterOrderingSpec(
                new QueryRequest(OrderBy: [new OrderClause("StockQuantity")]),
                products => products.Select(p => p.StockQuantity).ShouldBeInOrder()),

            ["ordering-desc"] = new FilterOrderingSpec(
                new QueryRequest(OrderBy: [new OrderClause("StockQuantity", true)]),
                products => products.Select(p => p.StockQuantity).ShouldBeInOrder(SortDirection.Descending)),

            ["multiple-orderby"] = new FilterOrderingSpec(
                new QueryRequest(OrderBy: [new OrderClause("Price"), new OrderClause("StockQuantity", true)]),
                products =>
                {
                    var sorted = products
                        .OrderBy(p => p.Price)
                        .ThenByDescending(p => p.StockQuantity)
                        .ToList();
                    products.ShouldBe(sorted);
                }),

            ["filter-order-include"] = new FilterOrderingSpec(
                new QueryRequest(
                    Filters: [new FilterClause("StockQuantity", "gt", "25")],
                    OrderBy: [new OrderClause("StockQuantity")],
                    Includes: ["Reviews"],
                    UseSplitQuery: true,
                    AsNoTracking: true),
                products =>
                {
                    products.Count.ShouldBe(2);
                    products.Select(p => p.StockQuantity).ShouldBeInOrder();
                    products[0].Reviews.ShouldNotBeNull();
                    products[1].Reviews.ShouldNotBeNull();
                }),

            ["filter-no-results"] = new FilterOrderingSpec(
                new QueryRequest(Filters: [new FilterClause("StockQuantity", "gt", "1000")]),
                products => products.Count.ShouldBe(0)),

            ["multiple-filters"] = new FilterOrderingSpec(
                new QueryRequest(Filters: [new FilterClause("StockQuantity", "gt", "25"), new FilterClause("Price", "lt", "50")]),
                products =>
                {
                    products.Count.ShouldBe(1);
                    products[0].StockQuantity.ShouldBeGreaterThan(25);
                    products[0].Price.ShouldBeLessThan(50);
                })
        };

    // CaseIdsFrom is defined in GetAllAsyncTests.Helpers.cs
}
