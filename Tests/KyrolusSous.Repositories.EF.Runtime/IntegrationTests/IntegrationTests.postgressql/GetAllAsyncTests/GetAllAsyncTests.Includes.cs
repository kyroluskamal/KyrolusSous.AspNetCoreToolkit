namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    [Fact(DisplayName = "GetAllAsync returns entities with Include Properties")]
    public async Task GetAllAsync_IncludeProperties_ReturnsEntitiesWithIncludeProperties()
    {
        var (_, reviews, _) = await ArrangeAndActUseingHttpForListAsync<Review>(
            new QueryRequest(Includes: ["Product", "Customer"]));
        reviews.ShouldNotBeNull();
        reviews.ShouldHaveSingleItem();
        reviews[0].Product.ShouldNotBeNull();
        reviews[0].Customer.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetAllAsync returns entities with multiple Includes")]
    public async Task GetAllAsync_MultipleIncludeGraphs_ReturnsEntitiesWithMultipleIncludeGraphs()
    {
        var (_, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(
            new QueryRequest(Includes: ["ProductCategories.Category", "OrderLines.Order"]));
        // Assert
        products.ShouldNotBeNull();
        products[0].ProductCategories.ShouldNotBeNull();
        products[0].ProductCategories.First().Category.ShouldNotBeNull();
        products[0].OrderLines.ShouldNotBeNull();
        products[0].OrderLines.First().Order.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetAllAsync returns entities with Include Graphs and Include Properties")]
    public async Task GetAllAsync_With_IncludeGraphs_IncludeProperties()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        // Act
        var result = await repo.GetAllAsync(
                    filter: null,
                    orderBy: null,
                    includeProperties: ["Store", "", ""],
                    includeGraph: new IncludeGraph<Product>(x => x.Reviews),
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default);
        // Assert
        result.ShouldNotBeNull();
        result.First().Store.ShouldNotBeNull();
        result.First().Reviews.ShouldNotBeNull();
        result.ToArray()[1].ShouldNotBeNull();
        result.ToArray()[1].Store.ShouldNotBeNull();
        result.ToArray()[1].Reviews.Count.ShouldBe(0);
        result.ToArray()[2].ShouldNotBeNull();
        result.ToArray()[2].Store.ShouldNotBeNull();
        result.ToArray()[1].Reviews.Count.ShouldBe(0);
    }
    [Fact(DisplayName = "GetAllAsync ignores blank include strings and still applies valid includes")]
    public async Task GetAllAsync_BlankIncludeStrings_AreIgnored()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["", "   ", "Reviews", "ProductCategories", "OrderLines"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        counter.Count.ShouldBe(4, $"Expected 4 SQL commands with split query and 3 collections, got {counter.Count}");
        items.ShouldNotBeNull();
    }
}
