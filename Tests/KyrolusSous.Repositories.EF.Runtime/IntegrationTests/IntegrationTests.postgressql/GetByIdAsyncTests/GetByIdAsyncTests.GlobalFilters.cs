namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    [Fact(DisplayName = "GetByIdAsync respects global filters")]
    public async Task GetByIdAsync_GlobalFilter_Works()
    {
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(p => p.Price >= 1250m));
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var item = await repo.GetByIdAsync(Guid.Parse(productLaptopId));
        item.ShouldBeNull();
    }
}
