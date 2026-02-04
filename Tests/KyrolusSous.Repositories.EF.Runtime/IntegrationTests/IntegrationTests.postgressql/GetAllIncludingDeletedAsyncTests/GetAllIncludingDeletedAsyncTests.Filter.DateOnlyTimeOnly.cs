namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports eq operator for DateOnly properties")]
    public async Task GetAllIncludingDeletedAsync_DateOnly_Eq_Operator_Works()
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Count.ShouldBe(1);
            products[0].AddedIn.ShouldBe(new DateOnly(2024, 6, 15));
        }, new QueryRequest(Filters: [new FilterClause("AddedIn", "eq", "2024-06-15")]));

    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports eq operator for TimeOnly properties")]
    public async Task GetAllIncludingDeletedAsync_TimeOnly_Eq_Operator_Works()
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Count.ShouldBe(1);
            products[0].AddedAt.ShouldBe(new TimeOnly(10, 30));
        }, new QueryRequest(Filters: [new FilterClause("AddedAt", "eq", "10:30")]));

    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports gt operator for DateOnly properties")]
    public async Task GetAllIncludingDeletedAsync_DateOnly_Gt_Operator_Works()
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Count.ShouldBe(2);
            products.All(p => p.AddedIn > new DateOnly(2024, 7, 1)).ShouldBeTrue();
        }, new QueryRequest(Filters: [new FilterClause("AddedIn", "gt", "2024-07-01")]));

    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports eq operator for DateTime properties")]
    public async Task GetAllIncludingDeletedAsync_DateTime_Eq_Operator_Works()
        => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (response, products, content, _) =>
        {
            response.IsSuccessStatusCode.ShouldBeTrue(content);
            products.ShouldNotBeNull();
            products.Count.ShouldBe(3);
            products.All(p => p.DiscontinuedAt == new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)).ShouldBeTrue();
        }, new QueryRequest(Filters: [new FilterClause("DiscontinuedAt", "eq", "2025-12-31T00:00:00Z")]));
}
