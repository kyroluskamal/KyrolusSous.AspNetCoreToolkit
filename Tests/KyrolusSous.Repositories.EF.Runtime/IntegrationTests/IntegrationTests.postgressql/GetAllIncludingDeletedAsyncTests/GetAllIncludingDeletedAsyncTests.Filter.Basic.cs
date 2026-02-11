
namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    [Theory(DisplayName = "GetAllIncludingDeletedAsync returns all entities with no filters")]
    [InlineData(KeyType.Single)]
    [InlineData(KeyType.Composite)]
    public async Task GetAllIncludingDeletedAsync_NoFilter_ReturnsAll(KeyType keyType)
    {
        await TestSingleKey(keyType, (p) => p.Count.ShouldBe(3));
        await TestCompositeKey(keyType, (r) => r.Count.ShouldBe(3));
    }
    [Theory(DisplayName = "GetAllIncludingDeletedAsync returns entities with Assencding ordering")]
    [InlineData(KeyType.Single)]
    [InlineData(KeyType.Composite)]
    public async Task GetAllIncludingDeletedAsync_Ordering_Works(KeyType keyType)
    {
        await TestSingleKey(keyType, (p) => p.Select(p => p.StockQuantity).ShouldBeInOrder(), new QueryRequest(OrderBy: [new OrderClause(nameof(Product.StockQuantity))]));
        await TestCompositeKey(keyType, (r) => r.Select(r => r.Rating).ShouldBeInOrder(), new QueryRequest(OrderBy: [new OrderClause(nameof(Review.Rating))]));
    }
    [Theory(DisplayName = "GetAllIncludingDeletedAsync returns entities with descending ordering")]
    [InlineData(KeyType.Single)]
    [InlineData(KeyType.Composite)]
    public async Task GetAllIncludingDeletedAsync_DescendingOrdering_Works(KeyType keyType)
    {
        await TestSingleKey(keyType, (p) => p.Select(p => p.StockQuantity).ShouldBeInOrder(SortDirection.Descending),
            new QueryRequest(OrderBy: [new OrderClause(nameof(Product.StockQuantity), true)]));
        await TestCompositeKey(keyType, (r) => r.Select(r => r.Rating).ShouldBeInOrder(SortDirection.Descending),
            new QueryRequest(OrderBy: [new OrderClause(nameof(Review.Rating), true)]));
    }
    [Theory(DisplayName = "GetAllIncludingDeletedAsync uses more that one OrderBy clause")]
    [InlineData(KeyType.Single)]
    [InlineData(KeyType.Composite)]
    public async Task GetAllIncludingDeletedAsync_MultipleOrderBy_ReturnsEntitiesWithMultipleOrderBy(KeyType keyType)
    {
        await TestSingleKey(keyType, (p) => p.ShouldBe([.. p.OrderBy(p => p.Price).ThenByDescending(p => p.StockQuantity)])
        , new QueryRequest(OrderBy: [new OrderClause(nameof(Product.Price)), new OrderClause(nameof(Product.StockQuantity), true)]));
        await TestCompositeKey(keyType, (r) => r.ShouldBe([.. r.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt)])
        , new QueryRequest(OrderBy: [new OrderClause(nameof(Review.Rating)), new OrderClause(nameof(Review.CreatedAt), true)]));
    }
    [Theory(DisplayName = "GetAllIncludingDeletedAsync returns entities with gt Filter, ordering and Include Properties")]
    [InlineData(KeyType.Single)]
    [InlineData(KeyType.Composite)]
    public async Task GetAllIncludingDeletedAsync_FilteringOrderingDefaultIncludeProperties_ReturnsEntitiesWithFilteringOrderingAndDefaultIncludeProperties(KeyType keyType)
    {
        await TestSingleKey(keyType, p =>
        {
            p.Count.ShouldBe(2);
            p.All(p => p.StockQuantity > 25).ShouldBeTrue();
            p.Select(p => p.StockQuantity).ShouldBeInOrder();
            p[0].ProductCategories.ShouldNotBeNull();
            p[1].ProductCategories.ShouldNotBeNull();
            p[0].OrderLines.ShouldNotBeNull();
            p[1].OrderLines.ShouldNotBeNull();
            p[0].Reviews.ShouldNotBeNull(); p[1].Reviews.ShouldNotBeNull();
        }, new QueryRequest(Filters: [new FilterClause(nameof(Product.StockQuantity), "gt", "25")],
                        OrderBy: [new OrderClause(nameof(Product.StockQuantity))],
                        Includes: [nameof(Product.Reviews), "", nameof(Product.OrderLines), nameof(Product.ProductCategories)],
                        UseSplitQuery: true, AsNoTracking: true));
        await TestCompositeKey(keyType, r =>
        {
            r.Count.ShouldBe(1);
            r.All(r => r.Rating > 4).ShouldBeTrue();
            r.Select(r => r.Rating).ShouldBeInOrder();
            r[0].Product.ShouldNotBeNull();
            r[0].Customer.ShouldNotBeNull();
        }, new QueryRequest(Filters: [new FilterClause(nameof(Review.Rating), "gt", "4")],
                        OrderBy: [new OrderClause(nameof(Review.Rating))],
                        Includes: [nameof(Review.Product), nameof(Review.Customer)],
                        UseSplitQuery: true, AsNoTracking: true));
    }
    [Theory(DisplayName = "GetAllIncludingDeletedAsync returns entities with gt Filter that results in no entities")]
    [InlineData(KeyType.Single)]
    [InlineData(KeyType.Composite)]
    public async Task GetAllIncludingDeletedAsync_Filtering_ReturnsNoEntities(KeyType keyType)
    {
        await TestSingleKey(keyType, p => p.Count.ShouldBe(0), new QueryRequest(Filters: [new FilterClause(nameof(Product.StockQuantity), "gt", "1000")]));
        await TestCompositeKey(keyType, r => r.Count.ShouldBe(0), new QueryRequest(Filters: [new FilterClause(nameof(Review.Rating), "gt", "10")]));
    }
    [Theory(DisplayName = "GetAllIncludingDeletedAsync should use multiple filters (gt and lt)")]
    [InlineData(KeyType.Single)]
    [InlineData(KeyType.Composite)]
    public async Task GetAllIncludingDeletedAsync_MultipleFilters_ReturnsEntitiesWithMultipleFilters(KeyType keyType)
    {
        await TestSingleKey(keyType, p =>
        {
            p.Count.ShouldBe(1);
            p[0].StockQuantity.ShouldBeGreaterThan(25);
            p[0].Price.ShouldBeLessThan(50);
        }, new QueryRequest(Filters: [new FilterClause(nameof(Product.StockQuantity), "gt", "25"), new FilterClause(nameof(Product.Price), "lt", 50.ToString())]));
        await TestCompositeKey(keyType, r =>
        {
            r.Count.ShouldBe(1);
            r[0].Rating.ShouldBeGreaterThan(4);
        }, new QueryRequest(Filters: [new FilterClause(nameof(Review.Rating), "gt", "4")]));
    }
    [Theory(DisplayName = "GetAllIncludingDeletedAsync should use multiple filters (> and <)")]
    [InlineData(KeyType.Single)]
    [InlineData(KeyType.Composite)]
    public async Task GetAllIncludingDeletedAsync_MultipleFilters__ReturnsEntitiesWithMultipleFilters(KeyType keyType)
    {
        await TestSingleKey(keyType, p =>
        {
            p.Count.ShouldBe(1);
            p[0].StockQuantity.ShouldBeGreaterThan(25);
            p[0].Price.ShouldBeLessThan(50);
        }, new QueryRequest(Filters: [new FilterClause(nameof(Product.StockQuantity), ">", 25.ToString()), new FilterClause(nameof(Product.Price), "<", 50.ToString())]));
        await TestCompositeKey(keyType, r =>
        {
            r.Count.ShouldBe(1);
            r[0].Rating.ShouldBe(4);
        }, new QueryRequest(Filters: [new FilterClause(nameof(Review.Rating), ">", "3"), new FilterClause(nameof(Review.Rating), "<", "5")]));
    }
    private Task TestSingleKey(KeyType keyType, Action<List<Product>> assert, QueryRequest? request = null)
    {
        if (keyType == KeyType.Single)
            return WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, _, _) =>
                        {
                            products.ShouldNotBeNull();
                            assert(products);
                        }, request);
        return Task.CompletedTask;
    }
    private Task TestCompositeKey(KeyType keyType, Action<List<Review>> Assert, QueryRequest? request = null)
    {
        if (keyType == KeyType.Composite)
            return WithSoftDeletedAsync_CompositeKey<Review>(DataSeeder.ReviewLapTopKey, async (_, reviews, _, _, _) =>
            {
                reviews.ShouldNotBeNull();
                Assert(reviews);
            }, request);
        return Task.CompletedTask;
    }
}
