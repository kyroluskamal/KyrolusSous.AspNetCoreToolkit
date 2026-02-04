namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    [Fact(DisplayName = "GetAllIncludingDeletedAsync respects UseSplitQuery")]
    public async Task GetAllIncludingDeletedAsync_UseSplitQuery_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        var items = await repo.GetAllIncludingDeletedAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews", "OrderLines", "ProductCategories"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        counter.Count.ShouldBe(4, $"Expected 4 SQL commands when UseSplitQuery=true, got {counter.Count}");
        items.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync uses a single SQL command when UseSplitQuery is false")]
    public async Task GetAllIncludingDeletedAsync_UseSplitQuery_False_UsesSingleSqlCommand()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllIncludingDeletedAsync(
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

    [Fact(DisplayName = "GetAllIncludingDeletedAsync uses UseSplitQueryDefault from policy when useSplitQuery is null")]
    public async Task GetAllIncludingDeletedAsync_UseSplitQuery_Null_UsesPolicyDefault()
    {
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = true });
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllIncludingDeletedAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews", "OrderLines", "ProductCategories"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: null,
            cancellationToken: default);

        counter.Count.ShouldBe(4, $"Expected 4 SQL commands when UseSplitQuery=null and policy default true, got {counter.Count}");
        items.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync uses UseSplitQueryDefault=false when useSplitQuery is null")]
    public async Task GetAllIncludingDeletedAsync_UseSplitQuery_Null_UsesPolicyDefaultFalse()
    {
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = false });
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllIncludingDeletedAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews", "OrderLines", "ProductCategories"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: null,
            cancellationToken: default);

        counter.Count.ShouldBe(1, $"Expected 1 SQL command when UseSplitQuery=null and policy default false, got {counter.Count}");
        items.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync defaults UseSplitQuery to false when policy default is null")]
    public async Task GetAllIncludingDeletedAsync_UseSplitQuery_Null_UsesDefaultWhenPolicyNull()
    {
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = null });
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllIncludingDeletedAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews", "OrderLines", "ProductCategories"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: null,
            cancellationToken: default);

        counter.Count.ShouldBe(1, $"Expected 1 SQL command when UseSplitQuery=null and policy default null, got {counter.Count}");
        items.ShouldNotBeNull();
    }
}
