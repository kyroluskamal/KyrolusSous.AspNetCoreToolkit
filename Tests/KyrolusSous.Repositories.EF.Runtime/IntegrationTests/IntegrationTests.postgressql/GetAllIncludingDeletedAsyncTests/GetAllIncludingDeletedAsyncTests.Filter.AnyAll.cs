namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports any operator for collection navigation")]
    public async Task GetAllIncludingDeletedAsync_Any_Operator_Works()
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Count.ShouldBe(2);
            products.All(p => p.ProductCategories.Any(pc => pc.CategoryId == DataSeeder.categoryElectronicsId)).ShouldBeTrue();
        }, new QueryRequest(Filters: [new FilterClause("ProductCategories", "any", $"CategoryId = {DataSeeder.categoryElectronicsId}")], Includes: ["ProductCategories"]));

    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports all operator for collection navigation")]
    public async Task GetAllIncludingDeletedAsync_All_Operator_Works()
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Count.ShouldBe(1);
            products[0].Name.ShouldBe("Clean Code");
            products[0].ProductCategories.ShouldNotBeNull();
        }, new QueryRequest(Filters: [new FilterClause("ProductCategories", "all", $"CategoryId = {DataSeeder.categoryBooksId}")], Includes: ["ProductCategories"]));
}
