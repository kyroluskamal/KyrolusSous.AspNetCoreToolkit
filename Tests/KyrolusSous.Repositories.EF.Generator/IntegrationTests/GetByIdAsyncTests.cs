namespace KyrolusSous.Repositories.EF.Generator.IntegrationTests;

public class GetByIdAsyncTests(WebApplicationFactory<Program> factory) : KyrolusGeneratorFixture(factory)
{
    private static readonly string productLaptopId = "66666666-6666-6666-6666-666666666661";
    private static readonly string productHeadphonesId = "66666666-6666-6666-6666-666666666662";
    private static readonly string categoryElectronicsId = "55555555-5555-5555-5555-555555555551";

    private static readonly string[] CompositeKey_ProductReview = [productLaptopId, "77777777-7777-7777-7777-777777777772"];
    private static readonly string[] CompositeKey_ProductCategory = [productLaptopId, categoryElectronicsId];

    [Fact(DisplayName = "GetByIdAsync should return entity without Include Properties and with single key")]
    public async Task GetByIdAsync_Returng_Entity_NoIncludeProperties_SingleKey()
    {
        var (response, product, _) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, string>(productHeadphonesId);
        response.EnsureSuccessStatusCode();
        product.ShouldNotBeNull();
        product.Name.ShouldBe("Noise Cancelling Headphones");
    }
    [Fact(DisplayName = "GetByIdAsync should return entity without Include Properties and with composite key")]
    public async Task GetByIdAsync_Returng_Entity_NoIncludeProperties_CompositeKey()
    {
        var (response, review, _) = await ArrangeAndActUseingHttpForGetByIdAsync_CompositeKey<Review>(CompositeKey_ProductReview);
        response.EnsureSuccessStatusCode();
        review.ShouldNotBeNull();
        review.Rating.ShouldBe(5);
    }

    #region Include Tests
    [Fact(DisplayName = "GetByIdAsync returns entity with Include Properties with single Key")]
    public async Task GetByIdAsync_ReturnsEntiy_IncludeProperties_SingleKey()
    {
        var (_, product, _) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, string>(productLaptopId,
            new QueryRequest(Includes: ["Store"]));
        product.ShouldNotBeNull();
        product.Store.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetByIdAsync returns entity with Include Properties with Composite Key")]
    public async Task GetByIdAsync_ReturnsEntiy_IncludeProperties_CompositeKey()
    {
        var (_, review, _) = await ArrangeAndActUseingHttpForGetByIdAsync_CompositeKey<Review>(CompositeKey_ProductReview,
            new QueryRequest(Includes: ["Product", "Customer"]));
        review.ShouldNotBeNull();
        review.Product.ShouldNotBeNull();
        review.Customer.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetByIdAsync returns entity with multiple Includes single key")]
    public async Task GetByIdAsync_MultipleIncludeGraphs_SingleKey()
    {
        var (_, product, _) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, string>(productHeadphonesId,
            new QueryRequest(Includes: ["ProductCategories.Category", "OrderLines.Order"]));
        // Assert
        product.ShouldNotBeNull();
        product.ProductCategories.ShouldNotBeNull();
        product.ProductCategories.First().Category.ShouldNotBeNull();
        product.OrderLines.ShouldNotBeNull();
        product.OrderLines.First().Order.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetByIdAsync returns entity with multiple Includes Composite key")]
    public async Task GetByIdAsync_MultipleIncludeGraphs_CompositeKey()
    {
        var (_, review, _) = await ArrangeAndActUseingHttpForGetByIdAsync_CompositeKey<Review>(CompositeKey_ProductReview,
            new QueryRequest(Includes: ["Product.Store", "Customer.Store"]));
        // Assert
        review.ShouldNotBeNull();
        review.Product.ShouldNotBeNull();
        review.Product.Store.ShouldNotBeNull();
        review.Customer.ShouldNotBeNull();
        review.Customer.Store.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetByIdAsync returns entity with Include Graphs and Include Properties with single key")]
    public async Task GetByIdAsync_With_IncludeGraphs_IncludeProperties_SingleKey()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        // Act
        var product = await repo.GetByIdAsync(
                    Guid.Parse(productLaptopId),
                    includeProperties: ["Store", "", ""],
                    includeGraph: new IncludeGraph<Product>(x => x.Reviews),
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default);
        // Assert
        product.ShouldNotBeNull();
        product.Store.ShouldNotBeNull();
        product.Reviews.ShouldNotBeNull();
        product.Reviews.Count.ShouldBe(1);
    }
    [Fact(DisplayName = "GetByIdAsync returns entity with Include Graphs and Include Properties with Composite key")]
    public async Task GetByIdAsync_With_IncludeGraphs_IncludeProperties_CompositeKey()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ReviewRepository>();
        // Act
        var Review = await repo.GetByIdAsync(
                    CompositeKey_ProductReview,
                    includeProperties: ["Customer", "", ""],
                    includeGraph: new IncludeGraph<Review>(x => x.Product, x => x.Customer!.Store),
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default);
        // Assert
        Review.ShouldNotBeNull();
        Review.Product.ShouldNotBeNull();
        Review.Product.Reviews.ShouldNotBeNull();
        Review.Customer.ShouldNotBeNull();
        Review.Customer.Store.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetByIdAsync ignores blank include strings and still applies valid includes - single key")]
    public async Task GetByIdAsync_BlankIncludeStrings_AreIgnored_SingleKey()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var products = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            includeProperties: ["", "   ", "Reviews"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        counter.Count.ShouldBe(4, $"Expected 4 SQL commands with split query and 3 collections, got {counter.Count}");
        products.ShouldNotBeNull();
        products.Reviews.ShouldNotBeNull();
        products.Reviews.Count.ShouldBe(1);
    }
    [Fact(DisplayName = "GetByIdAsync ignores blank include strings and still applies valid includes - Composite key")]
    public async Task GetByIdAsync_BlankIncludeStrings_AreIgnored_CompositeKey()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ReviewRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var review = await repo.GetByIdAsync(
            CompositeKey_ProductReview,
            includeProperties: ["", "   ", "Customer"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        counter.Count.ShouldBe(1, $"Expected 1 SQL commands and no collections with split query, got {counter.Count}");
        review.ShouldNotBeNull();
    }
    #endregion
    #region AsNoTracking Tests
    [Fact(DisplayName = "GetByIdAsync returns entity with AsNoTracking = true - single key")]
    public async Task GetByIdAsync_AsNoTracking_ReturnsEntityWithAsNoTracking_SingleKey()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        dbContext.ChangeTracker.Clear();
        // Act
        await repo.GetByIdAsync(
                    Guid.Parse(productLaptopId),
                    includeProperties: null,
                    includeGraph: null,
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default);
        // Assert
        dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }
    [Fact(DisplayName = "GetByIdAsync returns entity with AsNoTracking = true - Composite key")]
    public async Task GetByIdAsync_AsNoTracking_ReturnsEntityWithAsNoTracking_CompositeKey()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ReviewRepository>();
        dbContext.ChangeTracker.Clear();
        // Act
        await repo.GetByIdAsync(
                    CompositeKey_ProductReview,
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken: default);
        // Assert
        dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }
    [Fact(DisplayName = "GetByIdAsync returns entity with AsNoTracking = false - single key")]
    public async Task GetByIdAsync_AsNoTrackingFalse_ReturnsEntityWithAsNoTrackingFalse_SingleKey()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();

        // Act
        dbContext.ChangeTracker.Clear();

        await repo.GetByIdAsync(
                    Guid.Parse(productLaptopId),
                    asNoTracking: false,
                    useSplitQuery: null,
                    cancellationToken: default);
        dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }
    [Fact(DisplayName = "GetByIdAsync returns entity with AsNoTracking = false - Composite key")]
    public async Task GetByIdAsync_AsNoTrackingFalse_ReturnsEntityWithAsNoTrackingFalse_CompositeKey()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ReviewRepository>();

        // Act
        dbContext.ChangeTracker.Clear();

        var review = await repo.GetByIdAsync(
                    CompositeKey_ProductReview,
                    asNoTracking: false,
                    useSplitQuery: null,
                    cancellationToken: default);
        review.ShouldNotBeNull();
        dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }
    [Fact(DisplayName = "GetByIdAsync returns entity with AsNoTracking = null and Policy.AsNoTrackingDefault == true - single key")]
    public async Task GetByIdAsync_UseSplitQueryDefaultFromPolicy_ReturnsEntityWithDefaultUseSplitQueryOption_SingleKey()
    {
        // Arrange
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = true });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        // Act
        dbContext.ChangeTracker.Clear();

        await repo.GetByIdAsync(
                    Guid.Parse(productLaptopId),
                    asNoTracking: null,
                    useSplitQuery: null,
                    cancellationToken: default);
        dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }
    [Fact(DisplayName = "GetByIdAsync returns entity with AsNoTracking = null and Policy.AsNoTrackingDefault == true - Composite key")]
    public async Task GetByIdAsync_UseSplitQueryDefaultFromPolicy_ReturnsEntityWithDefaultUseSplitQueryOption_CompositeKey()
    {
        // Arrange
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = true });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ReviewRepository>();
        // Act
        dbContext.ChangeTracker.Clear();

        await repo.GetByIdAsync(
                    CompositeKey_ProductReview,
                    asNoTracking: null,
                    useSplitQuery: null,
                    cancellationToken: default);
        dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }
    [Fact(DisplayName = "GetByIdAsync returns entity with AsNoTracking = null and Policy.AsNoTrackingDefault == false - single key")]
    public async Task GetByIdAsync_UseAsNoTrackingDefaultFromPolicy_ReturnsEntityWithAsNoTrackingOptionFromPolicy_SingleKey()
    {
        // Arrange
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = false });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        // Act
        dbContext.ChangeTracker.Clear();

        await repo.GetByIdAsync(
                    Guid.Parse(productLaptopId),
                    asNoTracking: null,
                    useSplitQuery: null,
                    cancellationToken: default);
        dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }
    [Fact(DisplayName = "GetByIdAsync returns entity with AsNoTracking = null and Policy.AsNoTrackingDefault == false - Composite key")]
    public async Task GetByIdAsync_UseAsNoTrackingDefaultFromPolicy_ReturnsEntityWithAsNoTrackingOptionFromPolicy_CompositeKey()
    {
        // Arrange
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = false });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ReviewRepository>();
        // Act
        dbContext.ChangeTracker.Clear();

        await repo.GetByIdAsync(
                    CompositeKey_ProductReview,
                    asNoTracking: null,
                    useSplitQuery: null,
                    cancellationToken: default);
        dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }
    #endregion
    #region UseSplitQuery Tests
    [Fact(DisplayName = "GetByIdAsync returns entity and Policy.AsNoTrackingDefault == null - Single key and AsNoTracking in attribute = false")]
    public async Task GetByIdAsync_AsNoTracking_Null_UsesDefaultPolicy_AsNoTrackingDefault_Null_AsNoTrackingAttribute_True_SingleKey()
    {
        // Arrange
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = null });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        // Act
        dbContext.ChangeTracker.Clear();

        await repo.GetByIdAsync(
                    Guid.Parse(productLaptopId),
                    asNoTracking: null,
                    cancellationToken: default);
        dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }
    [Fact(DisplayName = "GetByIdAsync returns entity and Policy.AsNoTrackingDefault == null and AsNoTracking in attribute = true - Composite key")]
    public async Task GetByIdAsync_AsNoTracking_Null_UsesDefaultPolicy_AsNoTrackingDefault_Null_AsNoTrackingAttribute_True_CompositeKey()
    {
        // Arrange
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { AsNoTrackingDefault = null });
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ReviewRepository>();
        // Act
        dbContext.ChangeTracker.Clear();

        await repo.GetByIdAsync(
                    CompositeKey_ProductReview,
                    asNoTracking: null,
                    cancellationToken: default);
        dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact(DisplayName = "GetByIdAsync returns entity with UseSplitQuery option = true, policy.UseSplitQueryDefault = null and UseSplitQuery in Attribute = true- Single key")]
    public async Task GetByIdAsync_UseSplitQuery_True_InPolicy_Null_InAttribute_True_SingleKey()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();
        // Act
        counter.Reset();
        var items = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            includeProperties: ["Reviews"],
            useSplitQuery: true,
            cancellationToken: default);
        // Assert
        counter.Count.ShouldBe(4, $"Expected 4 SQL command, got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetByIdAsync returns entity with UseSplitQuery option = true, policy.UseSplitQueryDefault = null and UseSplitQuery in Attribute = true- Composite key")]
    public async Task GetByIdAsync_UseSplitQuery_True_InPolicy_Null_InAttribute_False_ComppositeKey()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ReviewRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();
        // Act
        counter.Reset();
        var items = await repo.GetByIdAsync(
            CompositeKey_ProductReview,
            includeProperties: ["Customer"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);
        // Assert
        counter.Count.ShouldBe(1, $"Expected 1 SQL command, got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetByIdAsync uses a single SQL command when UseSplitQuery is false (even with collection includes) - single key")]
    public async Task GetByIdAsync_UseSplitQuery_False_UsesSingleSqlCommand_SingleKey()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            includeProperties: ["Reviews"],
            useSplitQuery: false,
            cancellationToken: default);

        counter.Count.ShouldBe(1, $"Expected 1 SQL command when UseSplitQuery=false, got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetByIdAsync uses a single SQL command when UseSplitQuery is false (even with collection includes) - Composite key")]
    public async Task GetByIdAsync_UseSplitQuery_False_UsesSingleSqlCommand_CompositeKey()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductCategoryRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetByIdAsync(
           CompositeKey_ProductCategory,
            useSplitQuery: false,
            cancellationToken: default);

        counter.Count.ShouldBe(1, $"Expected 1 SQL command when UseSplitQuery=false, got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetByIdAsync uses useSplitQuery = null and policy with UseSplitQueryDefault = true, policy wins - single Key")]
    public async Task GetByIdAsync_UseSplitQuery_Null_UsesPolicy_UseSplitQueryDefault_True_SingleKey()
    {
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = true });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            useSplitQuery: null,
            cancellationToken: default);

        counter.Count.ShouldBe(3, $"Expected {3} SQL commands when UseSplitQuery=null (policy default), got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetByIdAsync uses useSplitQuery = null and policy with UseSplitQueryDefault = true, policy wins - Composite Key")]
    public async Task GetByIdAsync_UseSplitQuery_Null_UsesPolicy_UseSplitQueryDefault_Composite()
    {
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = true });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductCategoryRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetByIdAsync(
            CompositeKey_ProductCategory,
            useSplitQuery: null,
            cancellationToken: default);

        counter.Count.ShouldBe(1, $"Expected {1} SQL commands when UseSplitQuery=null (policy default), got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetByIdAsync uses useSplitQuery = null and policy with UseSplitQueryDefault == false, policy wins - single key")]
    public async Task GetByIdAsync_UseSplitQuery_Null_UsesPolicy_UseSplitQueryDefault_False_SingleKey()
    {
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = false });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            includeProperties: ["Reviews"],
            useSplitQuery: null);

        counter.Count.ShouldBe(1, $"Expected {1} SQL command when UseSplitQuery=null (policy default), got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetByIdAsync uses useSplitQuery = null and policy with UseSplitQueryDefault == false, policy wins - Composite key")]
    public async Task GetByIdAsync_UseSplitQuery_Null_UsesPolicy_UseSplitQueryDefault_False_CompositeKey()
    {
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = false });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ReviewRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetByIdAsync(CompositeKey_ProductReview, useSplitQuery: null);

        counter.Count.ShouldBe(1, $"Expected {1} SQL command when UseSplitQuery=null (policy default), got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetByIdAsync uses useSplitQuery = null and policy.UseSplitQueryDefault = null, Attribute wins - single key")]
    public async Task GetByIdAsync_UseSplitQuery_Null_UsesPolicy_UseSplitQueryDefault_Null_SingleKey()
    {
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = null });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            includeProperties: ["Reviews"],
            useSplitQuery: null);

        counter.Count.ShouldBe(4, $"Expected {4} SQL command when UseSplitQuery=null (policy default), got {counter.Count}");
        items.ShouldNotBeNull();
    }
    // [Fact(DisplayName = "GetByIdAsync uses useSplitQuery = true and policy.UseSplitQueryDefault = true, useSplitQuery wins ")]
    [Fact(DisplayName = "GetByIdAsync uses useSplitQuery = null and policy.UseSplitQueryDefault = null, Attribute wins - Composite key")]
    public async Task GetByIdAsync_UseSplitQuery_Null_UsesPolicy_UseSplitQueryDefault_Null_CompositeKey()
    {
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = null });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ReviewRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetByIdAsync(
            CompositeKey_ProductReview,
            useSplitQuery: null);

        counter.Count.ShouldBe(1, $"Expected {1} SQL command when UseSplitQuery=null (policy default), got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetByIdAsync uses useSplitQuery = true and policy.UseSplitQueryDefault = true, useSplitQuery wins - Single key")]
    public async Task GetByIdAsync_UseSplitQuery_True_UsesPolicy_UseSplitQueryDefault_True()
    {
        // Given
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = true });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        // When
        var items = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            includeProperties: ["Reviews"],
            useSplitQuery: true);
        // Then
        counter.Count.ShouldBe(4, $"Expected {4} SQL commands when UseSplitQuery=true, got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetByIdAsync uses useSplitQuery = true and policy.UseSplitQueryDefault = true, useSplitQuery wins - Single key")]
    public async Task GetByIdAsync_UseSplitQuery_True_UsesPolicy_UseSplitQueryDefault_True_CompositeKey()
    {
        // Given
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = true });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ReviewRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        // When
        var items = await repo.GetByIdAsync(
            CompositeKey_ProductReview,
            useSplitQuery: true);
        // Then
        counter.Count.ShouldBe(1, $"Expected {1} SQL commands when UseSplitQuery=true, got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetByIdAsync uses useSplitQuery = false and policy.UseSplitQueryDefault = true, useSplitQuery wins - Single key")]
    public async Task GetByIdAsync_UseSplitQuery_True_UsesPolicy_UseSplitQueryDefault_False_SingleKey()
    {
        // Given
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = true });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        // When
        var items = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            includeProperties: ["Reviews"],
            useSplitQuery: false);
        // Then
        counter.Count.ShouldBe(1, $"Expected {1} SQL commands when UseSplitQuery=true, got {counter.Count}");
        items.ShouldNotBeNull();
    }
    [Fact(DisplayName = "GetByIdAsync uses useSplitQuery = false and policy.UseSplitQueryDefault = true, useSplitQuery wins - Composite key")]
    public async Task GetByIdAsync_UseSplitQuery_True_UsesPolicy_UseSplitQueryDefault_False_CompositeKey()
    {
        // Given
        var Customfactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = true });
        using var scope = Customfactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ReviewRepository>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        // When
        var items = await repo.GetByIdAsync(
            CompositeKey_ProductReview,
            useSplitQuery: false);
        // Then
        counter.Count.ShouldBe(1, $"Expected {1} SQL commands when UseSplitQuery=true, got {counter.Count}");
        items.ShouldNotBeNull();
    }
    #endregion
    #region SoftDelete Tests
    [Fact(DisplayName = "GetByIdAsync does not return soft-deleted entity")]
    public async Task GetByIdAsync_DoesNotReturnSoftDeletedEntities()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();
        var UoW = scope.ServiceProvider.GetRequiredService<KyrolusUnitOfWork>();
        var product = await repo.GetByIdAsync(Guid.Parse(productLaptopId), asNoTracking: false);

        product.ShouldNotBeNull();
        try
        {
            await repo.SoftDeleteAsync(product.Id);
            await UoW.SaveChangesAsync();
            // Act
            var item = await repo.GetByIdAsync(product.Id, asNoTracking: false);

            // Assert
            item.ShouldBeNull();
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
    [Fact(DisplayName = "GetByIdAsync respects cancellation token")]
    public async Task GetByIdAsync_CanceledToken_ThrowsOperationCanceled()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await repo.GetByIdAsync(
                Guid.Parse(productLaptopId),
                includeProperties: ["Reviews"],
                includeGraph: null,
                asNoTracking: true,
                useSplitQuery: true,
                cancellationToken: cts.Token);
        });
    }
    #endregion
    #region Unhappy Path Tests
    [Fact(DisplayName = "GetByIdAsync throws when include string is invalid navigation")]
    public async Task GetByIdAsync_InvalidIncludeString_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProductRepository>();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await repo.GetByIdAsync(
                Guid.Parse(productLaptopId),
                includeProperties: ["NotARealNavigation"],
                includeGraph: null,
                asNoTracking: true,
                useSplitQuery: true,
                cancellationToken: default);
        });
    }
    [Fact(DisplayName = "GetByIdAsync should throw error for unsupported operator for String properties")]
    public async Task GetByIdAsync_Unsupported_String_FilterProperty_Throws()
    {
        var (_, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId), new QueryRequest(
            Filters: [new FilterClause("Name", "has", "Test")]
            ));
        content?.ShouldContain("Invalid filter: property='Name', operator='has', value='Test'");
    }
    [Fact(DisplayName = "GetByIdAsync returns entity with unsupported Numeric Filter operator throws")]
    public async Task GetByIdAsync_Unsupported_Numeric_FilterOperator_Throws()
    {
        var (_, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            Filters: [new FilterClause("StockQuantity", "has", 25.ToString())]
            ));
        content?.ShouldContain("Unsupported operator 'has'");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error for unsupported operator for Bool properties")]
    public async Task GetByIdAsync_BoolProperty_Unsupported_Operator_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            Filters: [new FilterClause("IsActive", "gt", "true")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Unsupported operator 'gt'");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error for unsupported operator for DateTimeOffset properties")]
    public async Task GetByIdAsync_DateTimeOffsetProperty_Unsupported_Operator_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            Filters: [new FilterClause("CreatedAt", "contains", "2024-06-01T00:00:00Z")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Unsupported operator 'contains'");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error for unsupported operator for Numeric properties")]
    public async Task GetByIdAsync_NumericProperty_Unsupported_Operator_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            Filters: [new FilterClause("StockQuantity", "contains", 25.ToString())]
            ));
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Unsupported operator 'contains'");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error for unsupported operator for Guid properties")]
    public async Task GetByIdAsync_GuidProperty_Unsupported_Operator_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            Filters: [new FilterClause("Id", "gt", "66666666-6666-6666-6666-666666666661")]
            ));
        // Then
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Unsupported operator 'gt'");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error for filter with invalid property name")]
    public async Task GetByIdAsync_InvalidFilterPropertyName_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            Filters: [new FilterClause("NotARealProperty", "eq", "SomeValue")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: property='NotARealProperty', operator='eq', value='SomeValue'");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error for filter with empty property name")]
    public async Task GetByIdAsync_EmptyFilterPropertyName_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
             Filters: [new FilterClause("", "eq", "SomeValue")]
             ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: 'Property' is required. (Parameter 'request')");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error for filter with null property name")]
    public async Task GetByIdAsync_NullFilterPropertyName_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            Filters: [new FilterClause(null!, "eq", "SomeValue")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: 'Property' is required. (Parameter 'request')");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error for filter with empty operator")]
    public async Task GetByIdAsync_EmptyFilterOperator_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            Filters: [new FilterClause("Name", "", "SomeValue")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter for property 'Name': 'Operator' is required.");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error for filter with null operator")]
    public async Task GetByIdAsync_NullFilterOperator_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            Filters: [new FilterClause("Name", null!, "SomeValue")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter for property 'Name': 'Operator' is required.");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error for ordering with invalid property")]
    public async Task GetByIdAsync_InvalidOrderByProperty_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            OrderBy: [new OrderClause("NotARealProperty")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid orderBy: property='NotARealProperty' not found on entity 'Product'");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error for ordering with empty property")]
    public async Task GetByIdAsync_EmptyOrderByProperty_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            OrderBy: [new OrderClause("")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid orderBy: 'Property' is required. (Parameter 'request')");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error for ordering with null property")]
    public async Task GetByIdAsync_NullOrderByProperty_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            OrderBy: [new OrderClause(null!)]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid orderBy: 'Property' is required. (Parameter 'request')");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error invalid numeric filter value")]
    public async Task GetByIdAsync_Invalid_NumericFilterValue_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            Filters: [new FilterClause("StockQuantity", "eq", "NotANumber")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: property='StockQuantity', operator='eq', value='NotANumber'");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error invalid Guid filter value")]
    public async Task GetByIdAsync_Invalid_GuidFilterValue_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            Filters: [new FilterClause("Id", "eq", "NotAGuid")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: property='Id', operator='eq', value='NotAGuid'");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error invalid DateTimeOffset filter value")]
    public async Task GetByIdAsync_Invalid_DateTimeOffsetFilterValue_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            Filters: [new FilterClause("CreatedAt", "eq", "NotADateTimeOffset")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: property='CreatedAt', operator='eq', value='NotADateTimeOffset'");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error invalid bool filter value")]
    public async Task GetByIdAsync_Invalid_BoolFilterValue_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            Filters: [new FilterClause("IsActive", "eq", "NotABool")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: property='IsActive', operator='eq', value='NotABool'");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error when 2 filter are applied one is valid and one invalid")]
    public async Task GetByIdAsync_OneValidOneInvalidFilter_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            Filters: [
                new FilterClause("Name", "contains", "Code"),
                new FilterClause("StockQuantity", "gt", "NotANumber")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: property='StockQuantity', operator='gt', value='NotANumber'");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error when 2 orderBy are applied one is valid and one invalid")]
    public async Task GetByIdAsync_OneValidOneInvalidOrderBy_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            OrderBy: [
                new OrderClause("Name"),
                new OrderClause("NotARealProperty")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid orderBy: property='NotARealProperty' not found on entity 'Product'");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error when both orderBy and filter have invalid properties")]
    public async Task GetByIdAsync_BothInvalidOrderByAndFilter_Throws()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            Filters: [new FilterClause("NotARealProperty", "eq", "SomeValue")],
            OrderBy: [new OrderClause("AlsoNotARealProperty")]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("Invalid filter: property='NotARealProperty', operator='eq', value='SomeValue'");
    }
    [Fact(DisplayName = "GetByIdAsync should throw error when Include string is Invalid navigation")]
    public async Task GetByIdAsync_InvalidIncludeString_Throws_InvalidNavigation()
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForGetByIdAsync_SingleKey<Product, Guid>(Guid.Parse(productLaptopId),new QueryRequest(
            Includes: ["Review", "NotARealNavigation"]
            ));
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content?.ShouldContain("InvalidInclude");
    }
    [Fact(DisplayName = "GetByIdAsync should not throw error QueryRequest is null")]
    public async Task GetByIdAsync_NullQueryRequest_Not_Throws()
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
