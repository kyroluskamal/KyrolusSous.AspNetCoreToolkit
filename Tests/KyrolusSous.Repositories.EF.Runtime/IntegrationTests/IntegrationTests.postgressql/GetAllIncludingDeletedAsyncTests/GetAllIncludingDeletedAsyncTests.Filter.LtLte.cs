namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    [Theory(DisplayName = "GetAllIncludingDeletedAsync should use (lte) operator for numeric properties")]
    [InlineData("lte")]
    [InlineData("<=")]
    public async Task GetAllIncludingDeletedAsync_NumericProperty_Lte_Operator_Works(string op)
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _, _) =>
        {
            products?.Count.ShouldBe(2);
            products?[1].StockQuantity.ShouldBeLessThanOrEqualTo(25);
            products?[0].StockQuantity.ShouldBeLessThanOrEqualTo(50);
            products.ShouldNotBeNull();
        }, new QueryRequest(Filters: [new FilterClause("StockQuantity", op, 50.ToString())]));
    [Theory(DisplayName = "GetAllIncludingDeletedAsync should uses (lt) operator Filter for DateTimeOffset properties")]
    [InlineData("lt")]
    [InlineData("<")]
    public async Task GetAllIncludingDeletedAsync_DateTimeOffsetProperty_Lt_Operator_Works(string op)
=> await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _, _) =>
{
    products.ShouldNotBeNull();
    products.Count.ShouldBe(1);
    products[0].CreatedAt.ShouldBeEquivalentTo(DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture));
}, new QueryRequest(Filters: [new FilterClause("CreatedAt", op, "2024-08-01T00:00:00Z")]));
    [Theory(DisplayName = "GetAllIncludingDeletedAsync should uses (lte) operator Filter for DateTimeOffset properties")]
    [InlineData("lte")]
    [InlineData("<=")]
    public async Task GetAllIncludingDeletedAsync_DateTimeOffsetProperty_Lte_Operator_Works(string op)
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _, _) =>
    {
        products.ShouldNotBeNull();
        products.Count.ShouldBe(2);
        products[0].CreatedAt.ShouldBeEquivalentTo(DateTimeOffset.Parse("2024-08-01T00:00:00Z", CultureInfo.InvariantCulture));
        products[1].CreatedAt.ShouldBeEquivalentTo(DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture));
    }, new QueryRequest(Filters: [new FilterClause("CreatedAt", op, "2024-08-01T00:00:00Z")]));
}
