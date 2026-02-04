namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    [Fact(DisplayName = "GetAllAsync throws when include string is invalid navigation")]
    public async Task GetAllAsync_InvalidIncludeString_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await repo.GetAllAsync(
                filter: null,
                orderBy: null,
                includeProperties: ["NotARealNavigation"],
                includeGraph: null,
                asNoTracking: true,
                useSplitQuery: true,
                cancellationToken: default);
        });
    }
    [Fact(DisplayName = "GetAllAsync should throw error for unsupported operator for String properties")]
    public async Task GetAllAsync_Unsupported_String_FilterProperty_Throws()
    {
        var (_, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("Name", "has", "Test")]
            ));
        content?.ShouldContain("Invalid filter: property='Name', operator='has', value='Test'");
    }
    [Fact(DisplayName = "GetAllAsync returns entities with unsupported Numeric Filter operator throws")]
    public async Task GetAllAsync_Unsupported_Numeric_FilterOperator_Throws()
    {
        var (_, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("StockQuantity", "has", 25.ToString())]
            ));
        content?.ShouldContain("Unsupported operator 'has'");
    }
    [Fact(DisplayName = "GetAllAsync should throw error for unsupported operator for Bool properties")]
    public async Task GetAllAsync_BoolProperty_Unsupported_Operator_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("IsActive", "gt", "true")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Unsupported operator 'gt'");
    }
    [Fact(DisplayName = "GetAllAsync should throw error for unsupported operator for DateTimeOffset properties")]
    public async Task GetAllAsync_DateTimeOffsetProperty_Unsupported_Operator_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("CreatedAt", "contains", "2024-06-01T00:00:00Z")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Unsupported operator 'contains'");
    }
    [Fact(DisplayName = "GetAllAsync should throw error for unsupported operator for Numeric properties")]
    public async Task GetAllAsync_NumericProperty_Unsupported_Operator_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("StockQuantity", "contains", 25.ToString())]
            ));
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Unsupported operator 'contains'");
    }
    [Fact(DisplayName = "GetAllAsync should throw error for unsupported operator for Guid properties")]
    public async Task GetAllAsync_GuidProperty_Unsupported_Operator_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("Id", "gt", "66666666-6666-6666-6666-666666666661")]
            ));
        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Unsupported operator 'gt'");
    }
    [Fact(DisplayName = "GetAllAsync should throw error for filter with invalid property name")]
    public async Task GetAllAsync_InvalidFilterPropertyName_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("NotARealProperty", "eq", "SomeValue")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: property='NotARealProperty', operator='eq', value='SomeValue'");
    }
    [Fact(DisplayName = "GetAllAsync should throw error for filter with empty property name")]
    public async Task GetAllAsync_EmptyFilterPropertyName_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
             Filters: [new FilterClause("", "eq", "SomeValue")]
             ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: 'Property' is required. (Parameter 'request')");
    }
    [Fact(DisplayName = "GetAllAsync should throw error for filter with null property name")]
    public async Task GetAllAsync_NullFilterPropertyName_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause(null!, "eq", "SomeValue")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: 'Property' is required. (Parameter 'request')");
    }
    [Fact(DisplayName = "GetAllAsync should throw error for filter with empty operator")]
    public async Task GetAllAsync_EmptyFilterOperator_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("Name", "", "SomeValue")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter for property 'Name': 'Operator' is required.");
    }
    [Fact(DisplayName = "GetAllAsync should throw error for filter with null operator")]
    public async Task GetAllAsync_NullFilterOperator_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("Name", null!, "SomeValue")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter for property 'Name': 'Operator' is required.");
    }
    [Fact(DisplayName = "GetAllAsync should throw error for ordering with invalid property")]
    public async Task GetAllAsync_InvalidOrderByProperty_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            OrderBy: [new OrderClause("NotARealProperty")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid orderBy: property='NotARealProperty' not found on entity 'Product'");
    }
    [Fact(DisplayName = "GetAllAsync should throw error for ordering with empty property")]
    public async Task GetAllAsync_EmptyOrderByProperty_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            OrderBy: [new OrderClause("")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid orderBy: 'Property' is required. (Parameter 'request')");
    }
    [Fact(DisplayName = "GetAllAsync should throw error for ordering with null property")]
    public async Task GetAllAsync_NullOrderByProperty_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            OrderBy: [new OrderClause(null!)]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid orderBy: 'Property' is required. (Parameter 'request')");
    }
    [Fact(DisplayName = "GetAllAsync should throw error invalid numeric filter value")]
    public async Task GetAllAsync_Invalid_NumericFilterValue_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("StockQuantity", "eq", "NotANumber")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: property='StockQuantity', operator='eq', value='NotANumber'");
    }
    [Fact(DisplayName = "GetAllAsync should throw error invalid Guid filter value")]
    public async Task GetAllAsync_Invalid_GuidFilterValue_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("Id", "eq", "NotAGuid")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: property='Id', operator='eq', value='NotAGuid'");
    }
    [Fact(DisplayName = "GetAllAsync should throw error invalid DateTimeOffset filter value")]
    public async Task GetAllAsync_Invalid_DateTimeOffsetFilterValue_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("CreatedAt", "eq", "NotADateTimeOffset")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: property='CreatedAt', operator='eq', value='NotADateTimeOffset'");
    }
    [Fact(DisplayName = "GetAllAsync should throw error invalid bool filter value")]
    public async Task GetAllAsync_Invalid_BoolFilterValue_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("IsActive", "eq", "NotABool")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: property='IsActive', operator='eq', value='NotABool'");
    }
    [Fact(DisplayName = "GetAllAsync should throw error when 2 filter are applied one is valid and one invalid")]
    public async Task GetAllAsync_OneValidOneInvalidFilter_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [
                new FilterClause("Name", "contains", "Code"),
                new FilterClause("StockQuantity", "gt", "NotANumber")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: property='StockQuantity', operator='gt', value='NotANumber'");
    }
    [Fact(DisplayName = "GetAllAsync should throw error when 2 orderBy are applied one is valid and one invalid")]
    public async Task GetAllAsync_OneValidOneInvalidOrderBy_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            OrderBy: [
                new OrderClause("Name"),
                new OrderClause("NotARealProperty")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid orderBy: property='NotARealProperty' not found on entity 'Product'");
    }
    [Fact(DisplayName = "GetAllAsync should throw error when both orderBy and filter have invalid properties")]
    public async Task GetAllAsync_BothInvalidOrderByAndFilter_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Filters: [new FilterClause("NotARealProperty", "eq", "SomeValue")],
            OrderBy: [new OrderClause("AlsoNotARealProperty")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: property='NotARealProperty', operator='eq', value='SomeValue'");
    }
    [Fact(DisplayName = "GetAllAsync should throw error when Include string is Invalid navigation")]
    public async Task GetAllAsync_InvalidIncludeString_Throws_InvalidNavigation()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(new QueryRequest(
            Includes: ["Review", "NotARealNavigation"]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("InvalidInclude");
    }
    [Fact(DisplayName = "GetAllAsync should not throw error QueryRequest is null")]
    public async Task GetAllAsync_NullQueryRequest_Not_Throws()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/product?request=null");
        // Act
        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        var items = JsonSerializer.Deserialize<List<Product>>(content, JsonOptions);
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        items.ShouldNotBeNull();
        items.Count.ShouldBe(3);
    }
}
