namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{

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

    [Fact(DisplayName = "GetAllAsync uses UseSplitQuery true with a single collection include, uses two SQL commands")]
    public async Task GetAllAsync_UseSplitQuery_True_WithSingleCollectionInclude_UsesTwoSqlCommands()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        counter.Count.ShouldBe(2, $"Expected 2 SQL commands when UseSplitQuery=true with one collection include, got {counter.Count}");
        items.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetAllAsync uses UseSplitQuery true with no includes, uses a single SQL command")]
    public async Task GetAllAsync_UseSplitQuery_True_WithNoIncludes_UsesSingleSqlCommand()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: null,
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        counter.Count.ShouldBe(1, $"Expected 1 SQL command when UseSplitQuery=true with no includes, got {counter.Count}");
        items.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetAllAsync uses UseSplitQuery true with reference include only, uses single SQL command")]
    public async Task GetAllAsync_UseSplitQuery_True_WithReferenceInclude_UsesSingleSqlCommand()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Store"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        counter.Count.ShouldBe(1, $"Expected 1 SQL command when UseSplitQuery=true with reference include, got {counter.Count}");
        items.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetAllAsync uses UseSplitQuery true with includeGraph collection and reference include, uses two SQL commands")]
    public async Task GetAllAsync_UseSplitQuery_True_WithIncludeGraphCollection_UsesTwoSqlCommands()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Store"],
            includeGraph: new IncludeGraph<Product>(x => x.Reviews),
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        counter.Count.ShouldBe(2, $"Expected 2 SQL commands when UseSplitQuery=true with one collection include, got {counter.Count}");
        items.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetAllAsync uses UseSplitQuery false with includeGraph collection and reference include, uses single SQL command")]
    public async Task GetAllAsync_UseSplitQuery_False_WithIncludeGraphCollection_UsesSingleSqlCommand()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Store"],
            includeGraph: new IncludeGraph<Product>(x => x.Reviews),
            asNoTracking: true,
            useSplitQuery: false,
            cancellationToken: default);

        counter.Count.ShouldBe(1, $"Expected 1 SQL command when UseSplitQuery=false, got {counter.Count}");
        items.ShouldNotBeNull();
    }
}
