namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    public static TheoryData<string, decimal, bool, int> GlobalFilterCases => new()
    {
        { "policy-only", 1250m, false, 0 },
        { "policy-plus-filter", 50m, true, 1 }
    };

    [Theory(DisplayName = "GetAllIncludingDeletedAsync respects global filters")]
    [MemberData(nameof(GlobalFilterCases))]
    public async Task GetAllIncludingDeletedAsync_GlobalFilter_Works(string caseId, decimal minPrice, bool useExplicitFilter, int expectedCount)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var policy = new KyrolusRepositoryPolicy()
            .AddGlobalWhereFilter<Product>(p => p.Price >= minPrice);

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        IReadOnlyList<Product> items;
        if (useExplicitFilter)
            items = await repo.GetAllIncludingDeletedAsync(p => p.StockQuantity > 25);
        else
            items = await repo.GetAllIncludingDeletedAsync();

        items.Count.ShouldBe(expectedCount);
    }
}
