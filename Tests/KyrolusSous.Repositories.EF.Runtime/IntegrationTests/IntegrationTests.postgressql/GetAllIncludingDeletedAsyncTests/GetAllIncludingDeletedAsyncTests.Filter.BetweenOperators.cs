namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports between operator for numeric values")]
    public async Task GetAllIncludingDeletedAsync_Between_Operator_Numeric_Works()
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Count.ShouldBe(1);
            products[0].Price.ShouldBe(199m);
        }, new QueryRequest(Filters: [new FilterClause("Price", "between", "100..300")]));

    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports between operator for DateOnly values")]
    public async Task GetAllIncludingDeletedAsync_Between_Operator_DateOnly_Works()
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Count.ShouldBe(2);
            products.All(p => p.AddedIn.Year == 2024).ShouldBeTrue();
        }, new QueryRequest(Filters: [new FilterClause("AddedIn", "between", "2024-06-01..2024-12-31")]));

    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports between operator for TimeOnly values")]
    public async Task GetAllIncludingDeletedAsync_Between_Operator_TimeOnly_Works()
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Count.ShouldBe(2);
            products.Select(p => p.AddedAt).ShouldAllBe(t => t >= new TimeOnly(9, 0) && t <= new TimeOnly(11, 0));
        }, new QueryRequest(Filters: [new FilterClause("AddedAt", "between", "09:00..11:00")]));
}
