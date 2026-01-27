using KyrolusSous.Repositories.EF.Abstractions;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql;

public class GetAllAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    [Fact(DisplayName = "GetAllAsync returns all entities without Include Properties or filters or ordering options")]
    public async Task GetAllAsync_NoIncludeNoFilterNoOrder_ReturnsAllEntities()
    {
        var (response, reviews, _) = await ArrangeAndActUseingHttpForListAsync<Review>();
        // Assert
        response.EnsureSuccessStatusCode();
        reviews.ShouldNotBeNull();
        reviews.ShouldHaveSingleItem();
    }
    #region Filter and Ordering Tests
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
    #endregion
    #region Global Filter Tests
    [Fact(DisplayName = "GetAllAsync uses global filter with multiple filters")]
    public async Task GetAllAsync_GlobalFilter_MultipleFilters_Works()
    {
        // Given
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(p => p.Price >= 50m));
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        // When
        var items = await repo.GetAllAsync(e => e.StockQuantity > 25);
        // Then
        items.ShouldNotBeNull();
        items.Count().ShouldBe(1);
        items.First().StockQuantity.ShouldBe(80);
    }
    #endregion
    #region Include Tests
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
    #endregion
    #region AsNoTracking Tests
    [Fact(DisplayName = "GetAllAsync returns entities with AsNoTracking = true")]
    public async Task GetAllAsync_AsNoTracking_ReturnsEntitiesWithAsNoTracking()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
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
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

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
    public async Task GetAllAsync_AsNoTracking_Null_AsNoTrackingDefaultFromPolicy_true()
    {
        // Arrange
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = true });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
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
    public async Task GetAllAsync_AsNoTracking_Null_AsNoTrackingDefaultFromPolicy_false()
    {
        // Arrange
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = false });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
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
    [Fact(DisplayName = "GetAllAsync uses default AsNoTracking when asNoTracking = null and Policy.AsNoTrackingDefault == null")]
    public async Task GetAllAsync_AsNoTracking_Null_AsNoTrackingDefault_Null_UsesDefaultPolicy()
    {
        // Arrange
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = null });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
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
    #endregion
    #region UseSplitQuery Tests

    [Fact(DisplayName = "GetAllAsync returns entities with UseSplitQuery true")]
    public async Task GetAllAsync_UseSplitQuery_true_ReturnsEntitiesWithUseSplitQuery()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();
        // Act
        counter.Reset();
        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews", "OrderLines", "ProductCategories"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);
        // Assert
        counter.Count.ShouldBe(4, $"Expected 4 SQL command, got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetAllAsync uses a single SQL command when UseSplitQuery is false (even with collection includes), method wins")]
    public async Task GetAllAsync_UseSplitQuery_False_UsesSingleSqlCommand()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews", "OrderLines", "ProductCategories"],
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
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews", "OrderLines", "ProductCategories"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: null,
            cancellationToken: default);

        counter.Count.ShouldBe(4, $"Expected {4} SQL commands when UseSplitQuery=null (policy default), got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetAllAsync uses useSplitQuery = null and policy with UseSplitQueryDefault == false, policy wins")]
    public async Task GetAllAsync_UseSplitQuery_Null_UsesPolicy_UseSplitQueryDefault_False()
    {
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = false });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews", "OrderLines", "ProductCategories"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: null,
            cancellationToken: default);

        counter.Count.ShouldBe(1, $"Expected {1} SQL command when mwthod useSplitQuery = null and policy with UseSplitQueryDefault == false, policy wins, got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetAllAsync uses useSplitQuery = null and policy.UseSplitQueryDefault = null, defaults to false")]
    public async Task GetAllAsync_UseSplitQuery_Null_UsesPolicy_UseSplitQueryDefault_Null()
    {
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = null });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews", "OrderLines", "ProductCategories"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: null,
            cancellationToken: default);

        counter.Count.ShouldBe(1, $"Expected {1} SQL command when useSplitQuery = null and policy.UseSplitQueryDefault = null, defaults to false, got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetAllAsync uses useSplitQuery = true and policy.UseSplitQueryDefault = true, method wins ")]
    public async Task GetAllAsync_UseSplitQuery_True_UsesPolicy_UseSplitQueryDefault_True()
    {
        // Given
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = true });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        // When
        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews", "OrderLines", "ProductCategories"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);
        // Then
        counter.Count.ShouldBe(4, $"Expected {4} SQL commands when UseSplitQuery=true, got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetAllAsync uses useSplitQuery = false and policy.UseSplitQueryDefault = true, useSplitQuery wins ")]
    public async Task GetAllAsync_UseSplitQuery_False_UsesPolicy_UseSplitQueryDefault_True()
    {
        // Given
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = true });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
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
    #region SoftDelete Tests
    [Fact(DisplayName = "GetAllAsync does not return soft-deleted entities")]
    public async Task GetAllAsync_DoesNotReturnSoftDeletedEntities()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var UoW = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        var product = await repo.GetByIdAsync(Guid.Parse("66666666-6666-6666-6666-666666666662"));

        product.ShouldNotBeNull();
        try
        {
            var x = await repo.SoftDeleteAsync(Guid.Parse("66666666-6666-6666-6666-666666666662"));
            var result = await UoW.SaveChangesAsync();
            x.ShouldBeTrue();
            result.ShouldBe(1);
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
    #region Cancellation Token Tests
    [Fact(DisplayName = "GetAllAsync respects cancellation token")]
    public async Task GetAllAsync_CanceledToken_ThrowsOperationCanceled()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

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
    #endregion
    #region NotifyBefore/NotifyAfter Tests
    [Fact(DisplayName = "GetAllAsync Should record Event before and After execution")]
    public async Task GetAllAsync_ShouldRecordEventBeforeAndAfterExecution()
    {
        // Given
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();
        // When
        await repo.GetAllAsync();
        // Then
        var beforeEvent = observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "GetAllAsync");
        beforeEvent.ShouldBe(1);
        var afterEvent = observer.Events.Count(e => e.Stage == ObserverState.After && e.Operation == "GetAllAsync");
        afterEvent.ShouldBe(1);
    }

    [Fact(DisplayName = "GetAllAsync Should notify after finishing with exception if there was an error")]
    public async Task ShouldNotifyAfterFinishingWithException()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();
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
        observer.Events.Count(e => e.Stage == ObserverState.After && e.Operation == "GetAllAsync" && e.Exception is not null).ShouldBe(1);
    }
    #endregion
    #region Cache tests
    // [Fact(DisplayName = "GetAllyAsync should be cached if the cache is enabled")]
    // public void TestName()
    // {
    //     // Given

    //     // When

    //     // Then
    // }
    #endregion
    #region Unhappy Path Tests
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
    #endregion
}
