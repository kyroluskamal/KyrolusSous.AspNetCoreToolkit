namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    [Fact(DisplayName = "GetAllIncludingDeletedAsync uses (startswith) operator Filter for string properties")]
    public async Task GetAllIncludingDeletedAsync_StringProperty_StartsWith_Operator_Works()
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
    {
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].Name.ShouldStartWith("Laptop");
    }, new QueryRequest(Filters: [new FilterClause("Name", "startswith", "Laptop")]));
    [Fact(DisplayName = "GetAllIncludingDeletedAsync uses (endswith) operator Filter for string properties")]
    public async Task GetAllIncludingDeletedAsync_StringProperty_EndsWith_Operator_Works()
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
    {
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].Name.ShouldEndWith("Headphones");
    }, new QueryRequest(Filters: [new FilterClause("Name", "endswith", "Headphones")]));
    [Fact(DisplayName = "GetAllIncludingDeletedAsync uses (contains) operator Filter for string properties")]
    public async Task GetAllIncludingDeletedAsync_StringOperators_Works()
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
    {
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].Name.ShouldContain("Code");
    }, new QueryRequest(Filters: [new FilterClause("Name", "contains", "Code")]));
    [Fact(DisplayName = "GetAllIncludingDeletedAsync uses (contains) operator Filter for string properties (case Sensitive)")]
    public async Task GetAllIncludingDeletedAsync_StringProperty_Contains_CaseSensitive_Operator_Works()
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
    {
        products.ShouldNotBeNull();
        products.Count.ShouldBe(0);
    }, new QueryRequest(Filters: [new FilterClause("Name", "contains", "clean code")]));
}
