namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    readonly object[] ReviewKey = [DataSeeder.productLaptopId, DataSeeder.customerJaneId];
    [Fact(DisplayName = "GetAllIncludingDeletedAsync returns all entities with no filters")]
    public async Task GetAllIncludingDeletedAsync_NoFilter_ReturnsAll()
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, items, _, _) =>
        {
            items.ShouldNotBeNull();
            items.Count.ShouldBe(3);
        });
    [Fact(DisplayName = "GetAllIncludingDeletedAsync returns entities with Assencding ordering")]
    public async Task GetAllIncludingDeletedAsync_Ordering_Works()
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, items, _, _) =>
        {
            items.ShouldNotBeNull();
            items.Select(p => p.StockQuantity).ShouldBeInOrder();
        }, new QueryRequest(OrderBy: [new OrderClause("StockQuantity")]));
    [Fact(DisplayName = "GetAllIncludingDeletedAsync returns entities with descending ordering")]
    public async Task GetAllIncludingDeletedAsync_DescendingOrdering_Works()
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, items, _, _) =>
        {
            items.ShouldNotBeNull();
            items.Select(p => p.StockQuantity).ShouldBeInOrder(SortDirection.Descending);
        }, new QueryRequest(OrderBy: [new OrderClause("StockQuantity", true)]));
    [Fact(DisplayName = "GetAllIncludingDeletedAsync uses more that one OrderBy clause")]
    public async Task GetAllIncludingDeletedAsync_MultipleOrderBy_ReturnsEntitiesWithMultipleOrderBy()
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
        {
            products.ShouldNotBeNull();
            var sortedProducts = products.OrderBy(p => p.Price).ThenByDescending(p => p.StockQuantity).ToList();
            products.ShouldBe(sortedProducts);
        }, new QueryRequest(OrderBy: [new OrderClause("Price"), new OrderClause("StockQuantity", true)]));
    [Fact(DisplayName = "GetAllIncludingDeletedAsync returns entities with gt Filter, ordering and Include Properties")]
    public async Task GetAllIncludingDeletedAsync_FilteringOrderingDefaultIncludeProperties_ReturnsEntitiesWithFilteringOrderingAndDefaultIncludeProperties()
=> await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
{
    products.ShouldNotBeNull();
    products.Count.ShouldBe(2);
    products.All(p => p.StockQuantity > 25).ShouldBeTrue();
    products.Select(p => p.StockQuantity).ShouldBeInOrder();
    products[0].ProductCategories.ShouldNotBeNull();
    products[1].ProductCategories.ShouldNotBeNull();
    products[0].OrderLines.ShouldNotBeNull();
    products[1].OrderLines.ShouldNotBeNull();
    products[0].Reviews.ShouldNotBeNull();
    products[1].Reviews.ShouldNotBeNull();
},
new QueryRequest(Filters: [new FilterClause("StockQuantity", "gt", "25")], OrderBy: [new OrderClause("StockQuantity")],
        Includes: ["Reviews", "", "OrderLines", "ProductCategories"], UseSplitQuery: true, AsNoTracking: true));
    [Fact(DisplayName = "GetAllIncludingDeletedAsync returns entities with gt Filter that results in no entities")]
    public async Task GetAllIncludingDeletedAsync_Filtering_ReturnsNoEntities()
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
        {
            products.ShouldNotBeNull();
            products.Count.ShouldBe(0);
        }, new QueryRequest(Filters: [new FilterClause("StockQuantity", "gt", "1000")]));
    [Fact(DisplayName = "GetAllIncludingDeletedAsync should use multiple filters (gt and lt)")]
    public async Task GetAllIncludingDeletedAsync_MultipleFilters_ReturnsEntitiesWithMultipleFilters()
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
        {
            products.ShouldNotBeNull();
            products.ShouldNotBeNull();
            products.Count.ShouldBe(1);
            products[0].StockQuantity.ShouldBeGreaterThan(25);
            products[0].Price.ShouldBeLessThan(50);
        }, new QueryRequest(Filters: [new FilterClause("StockQuantity", "gt", "25"), new FilterClause("Price", "lt", 50.ToString())]));
    [Fact(DisplayName = "GetAllIncludingDeletedAsync should use multiple filters (> and <)")]
    public async Task GetAllIncludingDeletedAsync_MultipleFilters__ReturnsEntitiesWithMultipleFilters()
    => await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _) =>
        {
            products.ShouldNotBeNull();
            products.ShouldNotBeNull();
            products.Count.ShouldBe(1);
            products[0].StockQuantity.ShouldBeGreaterThan(25);
            products[0].Price.ShouldBeLessThan(50);
        }, new QueryRequest(Filters: [new FilterClause("StockQuantity", ">", 25.ToString()), new FilterClause("Price", "<", 50.ToString())]));

}
