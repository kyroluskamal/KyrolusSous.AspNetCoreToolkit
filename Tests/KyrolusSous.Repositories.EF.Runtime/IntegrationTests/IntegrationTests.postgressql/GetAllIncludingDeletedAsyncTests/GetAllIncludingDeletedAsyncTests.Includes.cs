namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    [Fact(DisplayName = "GetAllIncludingDeletedAsync returns entities with includes")]
    public async Task GetAllIncludingDeletedAsync_Includes_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var items = await repo.GetAllIncludingDeletedAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews", "OrderLines", "ProductCategories"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        items.ShouldNotBeNull();
        items.Count.ShouldBe(3);
        items[0].Reviews.ShouldNotBeNull();
        items[0].OrderLines.ShouldNotBeNull();
        items[0].ProductCategories.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync ignores blank include strings and still applies valid includes")]
    public async Task GetAllIncludingDeletedAsync_BlankIncludeStrings_AreIgnored()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllIncludingDeletedAsync(
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

    [Fact(DisplayName = "GetAllIncludingDeletedAsync supports include graph with include properties")]
    public async Task GetAllIncludingDeletedAsync_IncludeGraph_With_IncludeProperties()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var items = await repo.GetAllIncludingDeletedAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Store"],
            includeGraph: new IncludeGraph<Product>(x => x.Reviews),
            asNoTracking: true,
            useSplitQuery: null,
            cancellationToken: default);

        items.ShouldNotBeNull();
        items.All(p => p.Store is not null).ShouldBeTrue();
        items.All(p => p.Reviews is not null).ShouldBeTrue();
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync applies default includes from policy")]
    public async Task GetAllIncludingDeletedAsync_DefaultIncludes_Applied()
    {
        var policy = new KyrolusRepositoryPolicy()
            .SetDefaultIncludeProperties<Product>("Store");

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var items = await repo.GetAllIncludingDeletedAsync(asNoTracking: true);
        items.Count.ShouldBe(3);
        items.All(p => p.Store is not null).ShouldBeTrue();
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync merges default includes with explicit includes when mode is Merge")]
    public async Task GetAllIncludingDeletedAsync_DefaultIncludes_Merge_Works()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultIncludeMode = KyrolusDefaultIncludeMode.Merge
        }.SetDefaultIncludeProperties<Product>("Store");

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var items = await repo.GetAllIncludingDeletedAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        items.All(p => p.Store is not null).ShouldBeTrue();
        items.All(p => p.Reviews is not null).ShouldBeTrue();
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync ignores default includes when mode is Replace and explicit includes exist")]
    public async Task GetAllIncludingDeletedAsync_DefaultIncludes_Replace_Works()
    {
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultIncludeMode = KyrolusDefaultIncludeMode.Replace
        }.SetDefaultIncludeProperties<Product>("Store");

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var items = await repo.GetAllIncludingDeletedAsync(
            filter: null,
            orderBy: null,
            includeProperties: ["Reviews"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        items.All(p => p.Store is null).ShouldBeTrue();
        items.All(p => p.Reviews is not null).ShouldBeTrue();
    }
}
