namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports in operator for numeric values")]
    public async Task GetAllIncludingDeletedAsync_In_Operator_Numeric_Works()
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Select(p => p.StockQuantity).OrderBy(x => x).ShouldBe([25, 50]);
        }, new QueryRequest(Filters: [new FilterClause("StockQuantity", "in", "25,50")]));

    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports in operator for string values")]
    public async Task GetAllIncludingDeletedAsync_In_Operator_String_Works()
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Select(p => p.Name).OrderBy(x => x).ShouldBe(["Clean Code", "Laptop Pro 15"]);
        }, new QueryRequest(Filters: [new FilterClause("Name", "in", "Laptop Pro 15|Clean Code")]));

    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports in operator for nullable decimal values")]
    public async Task GetAllIncludingDeletedAsync_In_Operator_NullableDecimal_Works()
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Count.ShouldBe(2);
            products.Count(p => p.Weight is null).ShouldBe(1);
            products.Any(p => p.Weight == 0.25m).ShouldBeTrue();
        }, new QueryRequest(Filters: [new FilterClause("Weight", "in", "null,0.25")]));

    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports in operator for nullable int values")]
    public async Task GetAllIncludingDeletedAsync_In_Operator_NullableInt_Works()
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Count.ShouldBe(2);
            products.Count(p => p.Count is null).ShouldBe(1);
            products.Any(p => p.Count == 10).ShouldBeTrue();
        }, new QueryRequest(Filters: [new FilterClause("Count", "in", "null,10")]));

    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports in operator for Guid values")]
    public async Task GetAllIncludingDeletedAsync_In_Operator_Guid_Works()
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _, _) =>
        {
            products.ShouldNotBeNull();
            var expected = new[] { DataSeeder.productHeadphonesId, DataSeeder.productLaptopId }
                .OrderBy(x => x)
                .ToArray();
            products.Select(p => p.Id).OrderBy(x => x).ShouldBe(expected);
        }, new QueryRequest(Filters: [new FilterClause("Id", "in", $"{DataSeeder.productLaptopId},{DataSeeder.productHeadphonesId}")]));
}
