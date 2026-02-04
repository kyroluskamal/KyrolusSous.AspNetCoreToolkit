namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    [Theory(DisplayName = "GetAllIncludingDeletedAsync should use (neq) operator for Bool properties")]
    [InlineData("neq")]
    [InlineData("!=")]
    [InlineData("<>")]
    public async Task GetAllIncludingDeletedAsync_BoolProperty_Neq_Operator_Works(string op)
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Count.ShouldBe(3);
            products.All(p => p.IsActive).ShouldBeTrue();
        }, new QueryRequest(Filters: [new FilterClause("IsActive", op, "false")]));
    [Theory(DisplayName = "GetAllIncludingDeletedAsync should use (neq) operator for Numeric properties")]
    [InlineData("neq")]
    [InlineData("!=")]
    [InlineData("<>")]
    public async Task GetAllIncludingDeletedAsync_NumericProperty_NotEq_Operator_Works(string op)
       => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
           {
               products.ShouldNotBeNull();
               products.Count.ShouldBe(2);
               products.All(p => p.StockQuantity != 25).ShouldBeTrue();
           }, new QueryRequest(Filters: [new FilterClause("StockQuantity", op, 25.ToString())]));
    [Theory(DisplayName = "GetAllIncludingDeletedAsync should uses (neq) operator Filter for Guid properties")]
    [InlineData("neq")]
    [InlineData("!=")]
    [InlineData("<>")]
    public async Task GetAllIncludingDeletedAsync_GuidProperty_Neq_Operator_Works(string op)
=> await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
{
    products.ShouldNotBeNull();
    products.Count.ShouldBe(2);
    products.Any(p => p.Id == DataSeeder.productLaptopId).ShouldBeFalse();
}, new QueryRequest(Filters: [new FilterClause("Id", op, DataSeeder.productLaptopId.ToString())]));
    [Theory(DisplayName = "GetAllIncludingDeletedAsync uses (neq) operator Filter for string properties")]
    [InlineData("neq")]
    [InlineData("!=")]
    [InlineData("<>")]
    public async Task GetAllIncludingDeletedAsync_StringProperty_Neq_Operator_Works(string op)
     => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
     {
         products.ShouldNotBeNull();
         products.Count.ShouldBe(2);
         products.Any(p => p.Name == "Clean Code").ShouldBeFalse();

     }, new QueryRequest(Filters: [new FilterClause("Name", op, "Clean Code")]));
}
