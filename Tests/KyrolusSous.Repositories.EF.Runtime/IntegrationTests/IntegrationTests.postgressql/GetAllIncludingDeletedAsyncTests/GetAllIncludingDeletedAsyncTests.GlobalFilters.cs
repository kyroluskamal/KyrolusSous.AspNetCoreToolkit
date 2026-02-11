namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    public sealed record GlobalFilterCase(
        string CaseId,
        decimal MinPrice,
        bool UseExplicitFilter,
        int ExpectedCount);

    public static TheoryData<GlobalFilterCase> GlobalFilterCases => new()
    {
        new GlobalFilterCase("policy-only", 1250m, false, 0),
        new GlobalFilterCase("policy-plus-filter", 50m, true, 1)
    };

    [Theory(DisplayName = "GetAllIncludingDeletedAsync respects global filters")]
    [MemberData(nameof(GlobalFilterCases))]
    public async Task GetAllIncludingDeletedAsync_GlobalFilter_Works(GlobalFilterCase testCase)
    {
        var policy = new KyrolusRepositoryPolicy()
            .AddGlobalWhereFilter<Product>(p => p.Price >= testCase.MinPrice);

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        IReadOnlyList<Product> items;
        if (testCase.UseExplicitFilter)
            items = await repo.GetAllIncludingDeletedAsync(p => p.StockQuantity > 25);
        else
            items = await repo.GetAllIncludingDeletedAsync();

        items.Count.ShouldBe(testCase.ExpectedCount);
    }
}
