namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports in/between operators via QueryHelper")]
    public async Task GetAllIncludingDeletedAsync_InBetween_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Product>>();

        var inParts = helper.Build(new QueryRequest(Filters: [new FilterClause("StockQuantity", "in", "25,50")]));
        var inItems = await repo.GetAllIncludingDeletedAsync(inParts.Filter);
        inItems.Select(p => p.StockQuantity).OrderBy(x => x).ShouldBe([25, 50]);

        var betweenParts = helper.Build(new QueryRequest(Filters: [new FilterClause("Price", "between", "100,300")]));
        var betweenItems = await repo.GetAllIncludingDeletedAsync(betweenParts.Filter);
        betweenItems.Count.ShouldBe(1);
        betweenItems[0].Price.ShouldBe(199m);
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports any/all operators via QueryHelper")]
    public async Task GetAllIncludingDeletedAsync_AnyAll_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Product>>();

        var anyParts = helper.Build(new QueryRequest(Filters: [new FilterClause("ProductCategories", "any", "CategoryId = 55555555-5555-5555-5555-555555555551")]));
        var anyItems = await repo.GetAllIncludingDeletedAsync(anyParts.Filter);
        anyItems.Count.ShouldBe(2);

        var allParts = helper.Build(new QueryRequest(Filters: [new FilterClause("ProductCategories", "all", "CategoryId = 55555555-5555-5555-5555-555555555552")]));
        var allItems = await repo.GetAllIncludingDeletedAsync(allParts.Filter);
        allItems.Count.ShouldBe(1);
        allItems[0].Name.ShouldBe("Clean Code");
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports isnull/notnull operators via QueryHelper")]
    public async Task GetAllIncludingDeletedAsync_IsNull_NotNull_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Product>>();

        var notNullParts = helper.Build(new QueryRequest(Filters: [new FilterClause("Store", "notnull", null)]));
        var notNullItems = await repo.GetAllIncludingDeletedAsync(notNullParts.Filter);
        notNullItems.Count.ShouldBe(3);

        var isNullParts = helper.Build(new QueryRequest(Filters: [new FilterClause("Store", "isnull", null)]));
        var isNullItems = await repo.GetAllIncludingDeletedAsync(isNullParts.Filter);
        isNullItems.Count.ShouldBe(0);
    }
}
