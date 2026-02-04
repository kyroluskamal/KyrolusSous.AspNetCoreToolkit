namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    [Fact(DisplayName = "GetAllIncludingDeletedAsync respects global filters")]
    public async Task GetAllIncludingDeletedAsync_GlobalFilter_Works()
    {
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(p => p.Price >= 1250m));
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var items = await repo.GetAllIncludingDeletedAsync();
        items.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync uses global filter with explicit filter")]
    public async Task GetAllIncludingDeletedAsync_GlobalFilter_WithFilter_Works()
    {
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(p => p.Price >= 50m));
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var items = await repo.GetAllIncludingDeletedAsync(p => p.StockQuantity > 25);
        items.Count.ShouldBe(1);
    }
}
