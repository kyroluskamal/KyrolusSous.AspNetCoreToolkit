namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    [Theory(DisplayName = "GetAllIncludingDeletedAsync should use (eq) operator for Numeric properties")]
    [InlineData("eq")]
    [InlineData("=")]
    [InlineData("==")]
    public async Task GetAllIncludingDeletedAsync_NumericProperty_Eq_Operator_Works(string op)
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Count.ShouldBe(1);
            products[0].StockQuantity.ShouldBe(25);
        }, new QueryRequest(Filters: [new FilterClause("StockQuantity", op, 25.ToString())]));

    [Theory(DisplayName = "GetAllIncludingDeletedAsync should use (eq) operator for Bool properties")]
    [InlineData("eq")]
    [InlineData("=")]
    [InlineData("==")]
    public async Task GetAllIncludingDeletedAsync_BoolProperty_Eq_Operator_Works(string op)
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
    {
        products.ShouldNotBeNull();
        products.Count.ShouldBe(0);
    }, new QueryRequest(Filters: [new FilterClause("IsActive", op, "false")]));
    [Theory(DisplayName = "GetAllIncludingDeletedAsync should uses (eq) operator Filter for DateTimeOffset properties")]
    [InlineData("eq")]
    [InlineData("=")]
    [InlineData("==")]
    public async Task GetAllIncludingDeletedAsync_DateTimeOffsetProperty_Eq_Operator_Works(string op)
=> await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
{
    products.ShouldNotBeNull();
    products.Count.ShouldBe(1);
    products[0].CreatedAt.ShouldBe(DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture));
}, new QueryRequest(Filters: [new FilterClause("CreatedAt", op, "2024-06-01T00:00:00Z")]));
    [Theory(DisplayName = "GetAllIncludingDeletedAsync uses (eq) operator Filter for string properties")]
    [InlineData("eq")]
    [InlineData("=")]
    [InlineData("==")]
    public async Task GetAllIncludingDeletedAsync_StringProperty_Eq_Operator_Works(string op)
=> await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
{
    products.ShouldNotBeNull();
    products.Count.ShouldBe(1);
    products[0].Name.ShouldBe("Clean Code");
}, new QueryRequest(Filters: [new FilterClause("Name", op, "Clean Code")]));
    [Theory(DisplayName = "GetAllIncludingDeletedAsync uses (eq) operator Filter for decimal properties")]
    [InlineData("eq")]
    [InlineData("=")]
    [InlineData("==")]
    public async Task GetAllIncludingDeletedAsync_DecimalProperty_Eq_Operator_Works(string op)
=> await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
{
    products.ShouldNotBeNull();
    products.Count.ShouldBe(1);
    products[0].Price.ShouldBe(199m);
}, new QueryRequest(Filters: [new FilterClause("Price", op, "199")]));
    [Theory(DisplayName = "GetAllIncludingDeletedAsync uses (eq) and (isnull) operator Filter for nullable decimal properties")]
    [InlineData("eq")]
    [InlineData("=")]
    [InlineData("==")]
    [InlineData("isnull")]
    public async Task GetAllIncludingDeletedAsync_Nullable_DecimalProperty_Eq_Operator_Works(string op)
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
    {
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].Weight.ShouldBeNull();
        products[0].Id.ShouldBe(DataSeeder.productLaptopId);
    }, new QueryRequest(Filters: [new FilterClause("Weight", op, "null")]));
    [Theory(DisplayName = "GetAllIncludingDeletedAsync uses (eq) and (isnull) operators Filter for nullable int properties")]
    [InlineData("eq")]
    [InlineData("=")]
    [InlineData("==")]
    [InlineData("isnull")]
    public async Task GetAllIncludingDeletedAsync_Nullable_IntProperty_Eq_Operator_Works(string op)
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
    {
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].Count.ShouldBeNull();
    }, new QueryRequest(Filters: [new FilterClause("Count", op, "null")]));
    [Theory(DisplayName = "GetAllIncludingDeletedAsync should throw if we use null with Nonnuallble it (eq) operator Filter for nullable int properties")]
    [InlineData("eq")]
    [InlineData("=")]
    [InlineData("==")]
    [InlineData("isnull")]
    public async Task GetAllIncludingDeletedAsync_Throw_NonNullable_IntProperty_Eq_Operator_Works(string op)
    => await WithSoftDeletedAsync_CompositeKey<Review>(ReviewKey, async (_, _, contents, _) =>
    {
        contents.ShouldNotBeNull();
        contents.ShouldContain($"Invalid filter for 'Rating': operator '{op}' ");
    }, new QueryRequest(Filters: [new FilterClause("Rating", op, "null")]));
    [Theory(DisplayName = "GetAllIncludingDeletedAsync should uses (eq) operator Filter for Guid properties")]
    [InlineData("eq")]
    [InlineData("=")]
    [InlineData("==")]
    public async Task GetAllIncludingDeletedAsync_GuidProperty_Eq_Operator_Works(string op)
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
    {
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].Id.ShouldBe(DataSeeder.productLaptopId);
    }, new QueryRequest(Filters: [new FilterClause("Id", op, DataSeeder.productLaptopId.ToString())]));


}
