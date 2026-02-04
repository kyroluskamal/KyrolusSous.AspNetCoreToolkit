namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    [Fact(DisplayName = "GetAllAsync returns entities with Assencding Ordering")]
    public async Task GetAllAsync_Ordering_ReturnsEntitiesWithOrdering()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(OrderBy: [new OrderClause("StockQuantity")]));
        // Assert
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Select(p => p.StockQuantity).ShouldBeInOrder();
    }
    [Fact(DisplayName = "GetAllAsync returns entities with Descending Ordering")]
    public async Task GetAllAsync_DescendingOrdering_ReturnsEntitiesWithDescendingOrdering()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(OrderBy: [new OrderClause("StockQuantity", true)]));
        // Assert
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Select(p => p.StockQuantity).ShouldBeInOrder(SortDirection.Descending);
    }
    [Fact(DisplayName = "GetAllAsync uses more that one OrderBy clause")]
    public async Task GetAllAsync_MultipleOrderBy_ReturnsEntitiesWithMultipleOrderBy()
    {
        var (_, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(OrderBy: [
            new OrderClause("Price"),
            new OrderClause("StockQuantity", true)
        ]));
        products.ShouldNotBeNull();
        var sortedProducts = products
            .OrderBy(p => p.Price)
            .ThenByDescending(p => p.StockQuantity)
            .ToList();
        products.ShouldBe(sortedProducts);
    }
    [Fact(DisplayName = "GetAllAsync returns entities with gt Filter, ordering and default and custom Include Properties")]
    public async Task GetAllAsync_FilteringOrderingDefaultIncludeProperties_ReturnsEntitiesWithFilteringOrderingAndDefaultIncludeProperties()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("StockQuantity", "gt", 25.ToString())],
            OrderBy: [new OrderClause("StockQuantity")],
            Includes: ["Reviews"],
            UseSplitQuery: true,
            AsNoTracking: true
            ));
        // Assert
        response.EnsureSuccessStatusCode();
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
    [Fact(DisplayName = "GetAllAsync returns entities with gt Filter that results in no entities")]
    public async Task GetAllAsync_Filtering_ReturnsNoEntities()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("StockQuantity", "gt", 1000.ToString())]
            ));
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(0);
    }
    [Fact(DisplayName = "GetAllAsync returns entities with multiple Filters (gt and lt)")]
    public async Task GetAllAsync_MultipleFilters_ReturnsEntitiesWithMultipleFilters()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
                    Filters: [
                        new FilterClause("StockQuantity", "gt", 25.ToString()),
                new FilterClause("Price", "lt", 50.ToString())
                    ]
                    ));
        // Assert
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].StockQuantity.ShouldBeGreaterThan(25);
        products[0].Price.ShouldBeLessThan(50);
    }
    #region eq operator
    [Fact(DisplayName = "GetAllAsync should use eq operator for Numeric properties")]
    public async Task GetAllAsync_NumericProperty_Eq_Operator_Works()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("StockQuantity", "eq", 25.ToString())]
            ));
        // Assert
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].StockQuantity.ShouldBe(25);
    }
    #endregion
    [Fact(DisplayName = "GetAllAsync should use gte operator for numeric properties")]
    public async Task GetAllAsync_NumericProperty_Gte_Operator_Works()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("StockQuantity", "gte", 50.ToString())]
            ));
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(2);
        products[0].StockQuantity.ShouldBeGreaterThanOrEqualTo(80);
        products[1].StockQuantity.ShouldBeGreaterThanOrEqualTo(50);
    }
    [Fact(DisplayName = "GetAllAsync should use lte operator for numeric properties")]
    public async Task GetAllAsync_NumericProperty_Lte_Operator_Works()
    {
        var (_, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("StockQuantity", "lte", 50.ToString())]
            ));
        products.ShouldNotBeNull();
        products.Count.ShouldBe(2);
        products[0].StockQuantity.ShouldBeLessThanOrEqualTo(25);
        products[1].StockQuantity.ShouldBeLessThanOrEqualTo(50);
    }
    [Fact(DisplayName = "GetAllAsync should use eq operator for Bool properties")]
    public async Task GetAllAsync_BoolProperty_Eq_Operator_Works()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("IsActive", "eq", "false")]
            ));
        // Assert
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllAsync should uses eq operator Filter for Guid properties")]
    public async Task GetAllAsync_GuidProperty_Eq_Operator_Works()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("Id", "eq", "66666666-6666-6666-6666-666666666661")]
            ));
        // Assert
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].Id.ShouldBe(Guid.Parse("66666666-6666-6666-6666-666666666661"));
    }
    [Fact(DisplayName = "GetAllAsync should uses eq operator Filter for DateTimeOffset properties")]
    public async Task GetAllAsync_DateTimeOffsetProperty_Eq_Operator_Works()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("CreatedAt", "eq", "2024-06-01T00:00:00Z")]
            ));
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].CreatedAt.ShouldBe(DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture));
    }
    [Fact(DisplayName = "GetAllAsync should uses gt operator Filter for DateTimeOffset properties")]
    public async Task GetAllAsync_DateTimeOffsetProperty_Gt_Operator_Works()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("CreatedAt", "gt", "2024-06-01T00:00:00Z")]
            ));
        // Then
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(2);
        products[0].CreatedAt.ShouldBeGreaterThan(DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture));
        products[1].CreatedAt.ShouldBeGreaterThan(DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture));
    }
    [Fact(DisplayName = "GetAllAsync should uses lt operator Filter for DateTimeOffset properties")]
    public async Task GetAllAsync_DateTimeOffsetProperty_Lt_Operator_Works()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("CreatedAt", "lt", "2024-08-01T00:00:00Z")]
            ));
        // Then
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].CreatedAt.ShouldBeEquivalentTo(DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture));
    }
    [Fact(DisplayName = "GetAllAsync should uses lte operator Filter for DateTimeOffset properties")]
    public async Task GetAllAsync_DateTimeOffsetProperty_Lte_Operator_Works()
    {
        var (_, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("CreatedAt", "lte", "2024-08-01T00:00:00Z")]
            ));
        products.ShouldNotBeNull();
        products.Count.ShouldBe(2);
        products[0].CreatedAt.ShouldBeEquivalentTo(DateTimeOffset.Parse("2024-06-01T00:00:00Z", CultureInfo.InvariantCulture));
        products[1].CreatedAt.ShouldBeEquivalentTo(DateTimeOffset.Parse("2024-08-01T00:00:00Z", CultureInfo.InvariantCulture));
    }
    [Fact(DisplayName = "GetAllAsync should uses gte operator Filter for DateTimeOffset properties")]
    public async Task GetAllAsync_DateTimeOffsetProperty_Gte_Operator_Works()
    {
        // Given
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("CreatedAt", "gte", "2024-08-01T00:00:00Z")]
            ));
        // Assert
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(2);
        products[0].CreatedAt.ShouldBeGreaterThanOrEqualTo(DateTimeOffset.Parse("2024-08-01T00:00:00Z", CultureInfo.InvariantCulture));
        products[1].CreatedAt.ShouldBeGreaterThanOrEqualTo(DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture));
    }
    [Fact(DisplayName = "GetAllAsync uses startswith operator Filter for string properties")]
    public async Task GetAllAsync_StringProperty_StartsWith_Operator_Works()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("Name", "startswith", "Laptop")]
            ));
        // Assert
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].Name.ShouldStartWith("Laptop");
    }
    [Fact(DisplayName = "GetAllAsync uses endswith operator Filter for string properties")]
    public async Task GetAllAsync_StringProperty_EndsWith_Operator_Works()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("Name", "endswith", "Headphones")]
            ));
        // Assert
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].Name.ShouldEndWith("Headphones");
    }
    [Fact(DisplayName = "GetAllAsync uses contains operator Filter for string properties")]
    public async Task GetAllAsync_StringProperty_Contains_Operator_Works()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("Name", "contains", "Code")]
            ));
        // Assert
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].Name.ShouldContain("Code");
    }
    [Fact(DisplayName = "GetAllAsync uses eq operator Filter for string properties")]
    public async Task GetAllAsync_StringProperty_Eq_Operator_Works()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("Name", "eq", "Clean Code")]
            ));
        // Assert
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].Name.ShouldBe("Clean Code");
    }
    [Fact(DisplayName = "GetAllAsync uses neq operator Filter for string properties")]
    public async Task GetAllAsync_StringProperty_Neq_Operator_Works()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("Name", "neq", "Clean Code")]
            ));
        // Assert
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(2);
        products.Any(p => p.Name == "Clean Code").ShouldBeFalse();
    }

    [Fact(DisplayName = "GetAllAsync uses in operator for numeric properties")]
    public async Task GetAllAsync_NumericProperty_In_Operator_Works()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("StockQuantity", "in", "25,50")]
            ));
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(2);
        products.Select(p => p.StockQuantity).OrderBy(x => x).ShouldBe([25, 50]);
    }

    [Fact(DisplayName = "GetAllAsync uses between operator for decimal properties")]
    public async Task GetAllAsync_DecimalProperty_Between_Operator_Works()
    {
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("Price", "between", "100,300")]
            ));
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].Price.ShouldBe(199m);
    }

    [Fact(DisplayName = "GetAllAsync uses any operator for collection properties")]
    public async Task GetAllAsync_CollectionProperty_Any_Operator_Works()
    {
        var electronicsId = "55555555-5555-5555-5555-555555555551";
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("ProductCategories", "any", $"CategoryId = {electronicsId}")]
            ));
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(2);
        products.Any(p => p.Name == "Clean Code").ShouldBeFalse();
    }

    [Fact(DisplayName = "GetAllAsync uses all operator for collection properties")]
    public async Task GetAllAsync_CollectionProperty_All_Operator_Works()
    {
        var booksId = "55555555-5555-5555-5555-555555555552";
        var (response, products, _) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("ProductCategories", "all", $"CategoryId = {booksId}")]
            ));
        response.EnsureSuccessStatusCode();
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].Name.ShouldBe("Clean Code");
    }

    [Fact(DisplayName = "GetAllAsync uses isnull operator for nullable properties")]
    public async Task GetAllAsync_NullableProperty_IsNull_Operator_Works()
    {
        var (response, payments, _) = await ArrangeAndActUseingHttpForListAsync<Payment>(new QueryRequest(
            Filters: [new FilterClause("PaidAt", "isnull", null)]
            ));
        response.EnsureSuccessStatusCode();
        payments.ShouldNotBeNull();
        payments.Count.ShouldBe(0);
    }

    [Fact(DisplayName = "GetAllAsync uses notnull operator for nullable properties")]
    public async Task GetAllAsync_NullableProperty_NotNull_Operator_Works()
    {
        var (response, payments, _) = await ArrangeAndActUseingHttpForListAsync<Payment>(new QueryRequest(
            Filters: [new FilterClause("PaidAt", "notnull", null)]
            ));
        response.EnsureSuccessStatusCode();
        payments.ShouldNotBeNull();
        payments.Count.ShouldBe(1);
        payments[0].PaidAt.ShouldNotBeNull();
    }
}
