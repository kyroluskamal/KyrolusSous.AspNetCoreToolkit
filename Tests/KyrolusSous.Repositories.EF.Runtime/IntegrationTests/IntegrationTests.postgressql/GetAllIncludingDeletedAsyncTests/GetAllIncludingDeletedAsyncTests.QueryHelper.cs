namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    public sealed record QueryHelperCase(
        string CaseId,
        FilterClause Filter,
        Action<IReadOnlyList<Product>> Assert);

    public static TheoryData<QueryHelperCase> QueryHelperCases => new()
    {
        new QueryHelperCase(
            "in",
            new FilterClause("StockQuantity", "in", "25,50"),
            items => items.Select(p => p.StockQuantity).OrderBy(x => x).ShouldBe([25, 50])),

        new QueryHelperCase(
            "between",
            new FilterClause("Price", "between", "100,300"),
            items =>
            {
                items.Count.ShouldBe(1);
                items[0].Price.ShouldBe(199m);
            }),

        new QueryHelperCase(
            "any",
            new FilterClause("ProductCategories", "any", $"CategoryId = {DataSeeder.categoryElectronicsId}"),
            items => items.Count.ShouldBe(2)),

        new QueryHelperCase(
            "all",
            new FilterClause("ProductCategories", "all", $"CategoryId = {DataSeeder.categoryBooksId}"),
            items =>
            {
                items.Count.ShouldBe(1);
                items[0].Name.ShouldBe("Clean Code");
            }),

        new QueryHelperCase(
            "notnull",
            new FilterClause("Store", "notnull", null),
            items => items.Count.ShouldBe(3)),

        new QueryHelperCase(
            "isnull",
            new FilterClause("Store", "isnull", null),
            items => items.Count.ShouldBe(0))
    };

    [Theory(DisplayName = "GetAllIncludingDeletedAsync supports QueryHelper filters")]
    [MemberData(nameof(QueryHelperCases))]
    public async Task GetAllIncludingDeletedAsync_QueryHelper_Works(QueryHelperCase testCase)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Product>>();

        var parts = helper.Build(new QueryRequest(Filters: [testCase.Filter]));
        var items = await repo.GetAllIncludingDeletedAsync(parts.Filter);
        testCase.Assert(items);
    }
}
