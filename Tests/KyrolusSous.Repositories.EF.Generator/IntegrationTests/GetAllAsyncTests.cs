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

    #region Filter and Ordering Tests
    [Fact(DisplayName = "GetAllAsync returns entities with Assencding Ordering")]
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
    [Fact(DisplayName = "GetAllAsync returns entities with Descending Ordering")]
    public async Task GetAllAsync_DescendingOrdering_ReturnsEntitiesWithDescendingOrdering()
    {

        // Arrange
        var queyrequest = new QueryRequest(OrderBy: [new OrderClause("StockQuantity", true)]);
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/product?request={JsonSerializer.Serialize(queyrequest, JsonOptions)}");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var orders = JsonSerializer.Deserialize<List<Product>>(content, JsonOptions);
        orders.ShouldNotBeNull();
        orders.Select(p => p.StockQuantity).ShouldBeInOrder(SortDirection.Descending);
    }
    [Fact(DisplayName = "GetAllAsync uses more that one OrderBy clause")]
    public async Task GetAllAsync_MultipleOrderBy_ReturnsEntitiesWithMultipleOrderBy()
    {
        // Arrange
        var queyrequest = new QueryRequest(OrderBy: [
            new OrderClause("Price"),
            new OrderClause("StockQuantity", true)
        ]);
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/product?request={JsonSerializer.Serialize(queyrequest, JsonOptions)}");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var products = JsonSerializer.Deserialize<List<Product>>(content, JsonOptions);
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
    [Fact(DisplayName = "GetAllAsync returns entities with Filter that results in no entities")]
    public async Task GetAllAsync_Filtering_ReturnsNoEntities()
    {

        // Arrange
        var queyrequest = new QueryRequest(
            Filters: [new FilterClause("StockQuantity", "gt", 1000.ToString())]
            );
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/product?request={JsonSerializer.Serialize(queyrequest, JsonOptions)}");
        // Act
        var response = await _client.SendAsync(request);
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var products = JsonSerializer.Deserialize<List<Product>>(content, JsonOptions);
        products.ShouldNotBeNull();
        products.Count.ShouldBe(0);
    }
    [Fact(DisplayName = "GetAllAsync returns entities with multiple Filters (gt and lt)")]
    public async Task GetAllAsync_MultipleFilters_ReturnsEntitiesWithMultipleFilters()
    {

        // Arrange
        var queyrequest = new QueryRequest(
            Filters: [
                new FilterClause("StockQuantity", "gt", 25.ToString()),
                new FilterClause("Price", "lt", 50.ToString())
            ],
            OrderBy: [new OrderClause("StockQuantity")]
            );
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/product?request={JsonSerializer.Serialize(queyrequest, JsonOptions)}");
        // Act
        var response = await _client.SendAsync(request);
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var products = JsonSerializer.Deserialize<List<Product>>(content, JsonOptions);
        products.ShouldNotBeNull();
        products.Count.ShouldBe(1);
        products[0].StockQuantity.ShouldBeGreaterThan(25);
        products[0].Price.ShouldBeLessThan(50);
    }
    [Fact(DisplayName = "GetAllAsync returns entities with unsupported Numeric Filter operator throws")]
    public async Task GetAllAsync_Unsupported_Numeric_FilterOperator_Throws()
    {
        // Given
        var queyrequest = new QueryRequest(
            Filters: [new FilterClause("StockQuantity", "has", 25.ToString())]
            );
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/product?request={JsonSerializer.Serialize(queyrequest, JsonOptions)}");
        // When
        var response = await _client.SendAsync(request);
        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("Unsupported operator 'has'");
    }
    [Fact(DisplayName = "GetAllAsync returns entities with unsupported String Filter property throws")]
    public async Task GetAllAsync_Unsupported_String_FilterProperty_Throws()
    {
        // Given
        var queyrequest = new QueryRequest(
            Filters: [new FilterClause("Name", "has", "Test")]
            );
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/product?request={JsonSerializer.Serialize(queyrequest, JsonOptions)}");
        // When
        var response = await _client.SendAsync(request);
        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("Invalid filter: property='Name', operator='has', value='Test'");
    }
    #endregion

    #region Include Tests
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
    [Fact(DisplayName = "GetAllAsync returns entities with multiple Includes")]
    public async Task GetAllAsync_MultipleIncludeGraphs_ReturnsEntitiesWithMultipleIncludeGraphs()
    {

        // Arrange
        var queyrequest = new QueryRequest(Includes: ["ProductCategories.Category", "OrderLines.Order"]);
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/product?request={JsonSerializer.Serialize(queyrequest, JsonOptions)}");

        // Act
        var response = await _client.SendAsync(request);
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var products = JsonSerializer.Deserialize<List<Product>>(content, JsonOptions);
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
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
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
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["", "   ", "Reviews"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        counter.Count.ShouldBe(4, $"Expected 4 SQL commands with split query and 3 collections, got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetAllAsync throws when include string is invalid navigation")]
    public async Task GetAllAsync_InvalidIncludeString_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();

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
    #endregion

    #region AsNoTracking Tests
    [Fact(DisplayName = "GetAllAsync returns entities with AsNoTracking = true")]
    public async Task GetAllAsync_AsNoTracking_ReturnsEntitiesWithAsNoTracking()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        dbContext.ChangeTracker.Clear();
        // Act
        await repo.GetAllAsync(
                    filter: null,
                    orderBy: null,
                    includeProperties: null,
                    includeGraph: null,
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default);
        // Assert
        dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }
    [Fact(DisplayName = "GetAllAsync returns entities with AsNoTracking = false")]
    public async Task GetAllAsync_AsNoTrackingFalse_ReturnsEntitiesWithAsNoTrackingFalse()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();

        // Act
        dbContext.ChangeTracker.Clear();

        await repo.GetAllAsync(
                    filter: null,
                    orderBy: null,
                    includeProperties: null,
                    includeGraph: null,
                    asNoTracking: false,
                    useSplitQuery: null,
                    cancellationToken: default);
        dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }
    [Fact(DisplayName = "GetAllAsync returns entities with AsNoTracking = null and Policy.AsNoTrackingDefault == true")]
    public async Task GetAllAsync_UseSplitQueryDefaultFromPolicy_ReturnsEntitiesWithDefaultUseSplitQueryOption()
    {
        // Arrange
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = true });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        // Act
        dbContext.ChangeTracker.Clear();

        await repo.GetAllAsync(
                    filter: null,
                    orderBy: null,
                    includeProperties: null,
                    includeGraph: null,
                    asNoTracking: null,
                    useSplitQuery: null,
                    cancellationToken: default);
        dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }
    [Fact(DisplayName = "GetAllAsync returns entities with AsNoTracking = null and Policy.AsNoTrackingDefault == false")]
    public async Task GetAllAsync_UseAsNoTrackingDefaultFromPolicy_ReturnsEntitiesWithAsNoTrackingOptionFromPolicy()
    {
        // Arrange
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = false });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        // Act
        dbContext.ChangeTracker.Clear();

        await repo.GetAllAsync(
                    filter: null,
                    orderBy: null,
                    includeProperties: null,
                    includeGraph: null,
                    asNoTracking: null,
                    useSplitQuery: null,
                    cancellationToken: default);
        dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }
    #endregion

    #region UseSplitQuery Tests
    [Fact(DisplayName = "GetAllAsync returns entities with UseSplitQuery = null and Policy.AsNoTrackingDefault == null")]
    public async Task GetAllAsync_AsNoTracking_Null_UsesDefaultPolicy_AsNoTrackingDefault_Null()
    {
        // Arrange
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = null });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        // Act
        dbContext.ChangeTracker.Clear();

        await repo.GetAllAsync(
                    filter: null,
                    orderBy: null,
                    includeProperties: null,
                    includeGraph: null,
                    asNoTracking: null,
                    useSplitQuery: null,
                    cancellationToken: default);
        dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }

    [Fact(DisplayName = "GetAllAsync returns entities with UseSplitQuery option")]
    public async Task GetAllAsync_UseSplitQuery_ReturnsEntitiesWithUseSplitQuery()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();
        // Act
        counter.Reset();
        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);
        // Assert
        counter.Count.ShouldBe(4, $"Expected 4 SQL command, got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetAllAsync uses a single SQL command when UseSplitQuery is false (even with collection includes)")]
    public async Task GetAllAsync_UseSplitQuery_False_UsesSingleSqlCommand()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: false,
            cancellationToken: default);

        counter.Count.ShouldBe(1, $"Expected 1 SQL command when UseSplitQuery=false, got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetAllAsync uses useSplitQuery = null and policy with UseSplitQueryDefault = true, policy wins")]
    public async Task GetAllAsync_UseSplitQuery_Null_UsesPolicy_UseSplitQueryDefault_True()
    {
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = true });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: null,
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: null,
            cancellationToken: default);

        counter.Count.ShouldBe(3, $"Expected {3} SQL commands when UseSplitQuery=null (policy default), got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetAllAsync uses useSplitQuery = null and policy with UseSplitQueryDefault == false, policy wins")]
    public async Task GetAllAsync_UseSplitQuery_Null_UsesPolicy_UseSplitQueryDefault_False()
    {
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = false });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: null,
            cancellationToken: default);

        counter.Count.ShouldBe(1, $"Expected {1} SQL command when UseSplitQuery=null (policy default), got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetAllAsync uses useSplitQuery = null and policy.UseSplitQueryDefault = null, default true wins")]
    public async Task GetAllAsync_UseSplitQuery_Null_UsesPolicy_UseSplitQueryDefault_Null()
    {
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = null });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: null,
            cancellationToken: default);

        counter.Count.ShouldBe(4, $"Expected {4} SQL command when UseSplitQuery=null (policy default), got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetAllAsync uses useSplitQuery = true and policy.UseSplitQueryDefault = true, useSplitQuery wins ")]
    public async Task GetAllAsync_UseSplitQuery_True_UsesPolicy_UseSplitQueryDefault_True()
    {
        // Given
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = true });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        // When
        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);
        // Then
        counter.Count.ShouldBe(4, $"Expected {4} SQL commands when UseSplitQuery=true, got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetAllAsync uses useSplitQuery = false and policy.UseSplitQueryDefault = true, useSplitQuery wins ")]
    public async Task GetAllAsync_UseSplitQuery_True_UsesPolicy_UseSplitQueryDefault_False()
    {
        // Given
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = true });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        // When
        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: false,
            cancellationToken: default);
        // Then
        counter.Count.ShouldBe(1, $"Expected {1} SQL commands when UseSplitQuery=true, got {counter.Count}");
        items.ShouldNotBeNull();
    }
    #endregion
    #region  SoftDelete Tests
    [Fact(DisplayName = "GetAllAsync does not return soft-deleted entities")]
    public async Task GetAllAsync_DoesNotReturnSoftDeletedEntities()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var UoW = scope.ServiceProvider.GetRequiredService<KyrolusUnitOfWork>();
        var product = await repo.GetByIdAsync(Guid.Parse("66666666-6666-6666-6666-666666666662"), asNoTracking: false);

        product.ShouldNotBeNull();
        try
        {
            await repo.RemoveAsync(product, isSoftDelete: true);
            await UoW.SaveChangesAsync();
            // Act
            var items = await repo.GetAllAsync(
                filter: null,
                orderBy: null,
                includeProperties: null,
                includeGraph: null,
                asNoTracking: true,
                useSplitQuery: null,
                cancellationToken: default);

            // Assert
            items.First().Id.ShouldNotBe(product.Id);
            items.Count().ShouldBe(2);
            items.Any(p => p.IsDeleted).ShouldBeFalse();
        }
        finally
        {
            if (product != null)
            {
                await repo.RestoreAsync(product.Id);
                await UoW.SaveChangesAsync();
            }
        }
    }
    #endregion
    [Fact(DisplayName = "GetAllAsync respects cancellation token")]
    public async Task GetAllAsync_CanceledToken_ThrowsOperationCanceled()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await repo.GetAllAsync(
                filter: null,
                orderBy: null,
                includeProperties: ["Reviews"],
                includeGraph: null,
                asNoTracking: true,
                useSplitQuery: true,
                cancellationToken: cts.Token);
        });
    }
}
