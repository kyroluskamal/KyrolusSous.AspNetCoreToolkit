namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    [Fact(DisplayName = "GetAllAsync uses global filter with multiple filters")]
    public async Task GetAllAsync_GlobalFilter_MultipleFilters_Works()
    {
        // Given
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(p => p.Price >= 50m));
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        // When
        var items = await repo.GetAllAsync(e => e.StockQuantity > 25);
        // Then
        items.ShouldNotBeNull();
        items.Count().ShouldBe(1);
        items.First().StockQuantity.ShouldBe(80);
    }
}
