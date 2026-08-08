namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    public static TheoryData<string, decimal, bool, int> GlobalFilterCases => new()
    {
        { "policy-only", 1250m, false, 0 },
        { "policy-plus-filter", 50m, true, 1 }
    };

    [Theory(DisplayName = "GetAllAsync respects global filters")]
    [MemberData(nameof(GlobalFilterCases))]
    public async Task GetAllAsync_GlobalFilter_Works(string caseId, decimal minPrice, bool useExplicitFilter, int expectedCount)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(p => p.Price >= minPrice));
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        IEnumerable<Product> items;
        if (useExplicitFilter)
            items = await repo.GetAllAsync(e => e.StockQuantity > 25);
        else
            items = await repo.GetAllAsync();

        items.ShouldNotBeNull();
        items.Count().ShouldBe(expectedCount);
        if (useExplicitFilter && expectedCount > 0)
            items.First().StockQuantity.ShouldBe(80);
    }
}
