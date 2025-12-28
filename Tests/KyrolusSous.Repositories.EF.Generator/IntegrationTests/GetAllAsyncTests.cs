using System.Numerics;

namespace KyrolusSous.Repositories.EF.Generator.IntegrationTests;

public class GetAllAsyncTests(WebApplicationFactory<Program> factory) : KyrolusGeneratorFixture(factory)
{

    [Fact(DisplayName = "GetAllAsync returns all entities without Include Properties or filters or ordering options")]
    public async Task GetAllAsync_NoIncludeNoFilterNoOrder_ReturnsAllEntities()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/review");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var reviews = JsonSerializer.Deserialize<List<Review>>(content, JsonOptions);

        reviews.ShouldNotBeNull();
        reviews.ShouldHaveSingleItem();
    }

    [Fact(DisplayName = "GetAllAsync returns entities with Include Properties")]
    public async Task GetAllAsync_IncludeProperties_ReturnsEntitiesWithIncludeProperties()
    {

        // Arrange
        var queyrequest = new QueryRequest(Includes: ["Product", "Customer"]);
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/review?request={JsonSerializer.Serialize(queyrequest, JsonOptions)}");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var reviews = JsonSerializer.Deserialize<List<Review>>(content, JsonOptions);

        reviews.ShouldNotBeNull();
        reviews.ShouldHaveSingleItem();
        reviews[0].Product.ShouldNotBeNull();
        reviews[0].Customer.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetAllAsync returns entities with Ordering")]
    public async Task GetAllAsync_Ordering_ReturnsEntitiesWithOrdering()
    {

        // Arrange
        var queyrequest = new QueryRequest(OrderBy: [new OrderClause("StockQuantity")]);
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/product?request={JsonSerializer.Serialize(queyrequest, JsonOptions)}");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var orders = JsonSerializer.Deserialize<List<Product>>(content, JsonOptions);
        orders.ShouldNotBeNull();
        orders.Select(p => p.StockQuantity).ShouldBeInOrder();
    }
    [Fact(DisplayName = "GetAllAsync returns entities with Filter, ordering and default and custom Include Properties")]
    public async Task GetAllAsync_FilteringOrderingDefaultIncludeProperties_ReturnsEntitiesWithFilteringOrderingAndDefaultIncludeProperties()
    {

        // Arrange
        var queyrequest = new QueryRequest(
            Filters: [new FilterClause("StockQuantity", "gt", 25.ToString())],
            OrderBy: [new OrderClause("StockQuantity")],
            Includes: ["Reviews"],
            UseSplitQuery: true,
            AsNoTracking: true
            );
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/product?request={JsonSerializer.Serialize(queyrequest, JsonOptions)}");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var products = JsonSerializer.Deserialize<List<Product>>(content, JsonOptions);
        products.ShouldNotBeNull();
        products.Count.ShouldBe(2);
        products.Select(p => p.StockQuantity).ShouldBeInOrder();
        products[0].ProductCategories.ShouldNotBeNull();
        products[1].ProductCategories.ShouldNotBeNull();
        products[0].OrderLines.ShouldNotBeNull();
        products[1].OrderLines.ShouldNotBeNull();
        products[0].Reviews.ShouldNotBeNull();
        products[1].Reviews.ShouldNotBeNull();
    }
}
