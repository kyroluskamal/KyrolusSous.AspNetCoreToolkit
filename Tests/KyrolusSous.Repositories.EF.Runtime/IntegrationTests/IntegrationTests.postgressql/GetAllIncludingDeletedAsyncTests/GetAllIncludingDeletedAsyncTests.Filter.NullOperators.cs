namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports isnull operator for nullable properties")]
    public async Task GetAllIncludingDeletedAsync_IsNull_Operator_Works()
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Count.ShouldBe(1);
            products[0].Weight.ShouldBeNull();
        }, new QueryRequest(Filters: [new FilterClause("Weight", "isnull", null)]));

    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports notnull operator for nullable properties")]
    public async Task GetAllIncludingDeletedAsync_NotNull_Operator_Works()
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Count.ShouldBe(2);
            products.All(p => p.Weight is not null).ShouldBeTrue();
        }, new QueryRequest(Filters: [new FilterClause("Weight", "notnull", null)]));

    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports notnull operator for reference navigation")]
    public async Task GetAllIncludingDeletedAsync_NotNull_Operator_Reference_Works()
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Count.ShouldBe(3);
        }, new QueryRequest(Filters: [new FilterClause("Store", "notnull", null)]));
}
