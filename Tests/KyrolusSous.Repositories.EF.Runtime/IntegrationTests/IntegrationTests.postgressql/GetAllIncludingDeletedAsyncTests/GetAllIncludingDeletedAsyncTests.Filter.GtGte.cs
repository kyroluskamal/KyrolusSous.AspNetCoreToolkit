namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    [Theory(DisplayName = "GetAllIncludingDeletedAsync should use (gte) operator for numeric properties")]
    [InlineData("gte")]
    [InlineData(">=")]
    public async Task GetAllIncludingDeletedAsync_NumericProperty_Gte_Operator_Works(string op)
     => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _, _) =>
         {
             products.ShouldNotBeNull();
             products.Count.ShouldBe(2);
             products[0].StockQuantity.ShouldBeGreaterThanOrEqualTo(80);
             products[1].StockQuantity.ShouldBeGreaterThanOrEqualTo(50);
         }, new QueryRequest(Filters: [new FilterClause("StockQuantity", op, 50.ToString())]));
    [Theory(DisplayName = "GetAllIncludingDeletedAsync should uses (gt) operator Filter for DateTimeOffset properties")]
    [InlineData("gt")]
    [InlineData(">")]
    public async Task GetAllIncludingDeletedAsync_DateTimeOffsetProperty_Gt_Operator_Works(string op)
=> await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _, _) =>
{
    products.ShouldNotBeNull();
    products.Count.ShouldBe(2);
    products[0].CreatedAt.ShouldBeGreaterThan(DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture));
    products[1].CreatedAt.ShouldBeGreaterThan(DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture));
}, new QueryRequest(Filters: [new FilterClause("CreatedAt", op, "2024-06-01T00:00:00Z")]));
    [Theory(DisplayName = "GetAllIncludingDeletedAsync should uses (gte) operator Filter for DateTimeOffset properties")]
    [InlineData("gte")]
    [InlineData(">=")]
    public async Task GetAllIncludingDeletedAsync_DateTimeOffsetProperty_Gte_Operator_Works(string op)
     => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _, _) =>
     {
         products.ShouldNotBeNull();
         products.Count.ShouldBe(2);
         products[0].CreatedAt.ShouldBeGreaterThanOrEqualTo(DateTimeOffset.Parse("2024-08-01T00:00:00Z", CultureInfo.InvariantCulture));
         products[1].CreatedAt.ShouldBeGreaterThanOrEqualTo(DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture));
     }, new QueryRequest(Filters: [new FilterClause("CreatedAt", op, "2024-08-01T00:00:00Z")]));
}
