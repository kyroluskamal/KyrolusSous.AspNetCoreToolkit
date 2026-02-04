namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    [Fact(DisplayName = "GetByIdAsync uses UseSplitQuery = true")]
    public async Task GetByIdAsync_UseSplitQuery_True()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        var item = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            includeProperties: ["Reviews", "OrderLines", "ProductCategories"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        counter.Count.ShouldBe(4, $"Expected 4 SQL commands when UseSplitQuery=true, got {counter.Count}");
        item.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetByIdAsync uses UseSplitQuery = false (method wins)")]
    public async Task GetByIdAsync_UseSplitQuery_False()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        var item = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            includeProperties: ["Reviews", "OrderLines", "ProductCategories"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: false,
            cancellationToken: default);

        counter.Count.ShouldBe(1, $"Expected 1 SQL command when UseSplitQuery=false, got {counter.Count}");
        item.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetByIdAsync uses policy UseSplitQueryDefault = true when useSplitQuery is null")]
    public async Task GetByIdAsync_UseSplitQuery_Null_PolicyTrue()
    {
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = true });
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        var item = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            includeProperties: ["Reviews", "OrderLines", "ProductCategories"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: null,
            cancellationToken: default);

        counter.Count.ShouldBe(4, $"Expected 4 SQL commands when UseSplitQuery=null (policy default), got {counter.Count}");
        item.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetByIdAsync uses policy UseSplitQueryDefault = false when useSplitQuery is null")]
    public async Task GetByIdAsync_UseSplitQuery_Null_PolicyFalse()
    {
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = false });
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        var item = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            includeProperties: ["Reviews", "OrderLines", "ProductCategories"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: null,
            cancellationToken: default);

        counter.Count.ShouldBe(1, $"Expected 1 SQL command when UseSplitQueryDefault=false, got {counter.Count}");
        item.ShouldNotBeNull();
    }
}
