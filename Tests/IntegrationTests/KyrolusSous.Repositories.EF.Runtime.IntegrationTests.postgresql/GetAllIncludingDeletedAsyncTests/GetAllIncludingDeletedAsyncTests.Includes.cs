namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    private sealed record IncludePolicySpec(
        KyrolusDefaultIncludeMode? Mode,
        string[]? IncludeProperties,
        bool? ExpectStore,
        bool? ExpectReviews);

    private static readonly IReadOnlyDictionary<string, IncludePolicySpec> IncludePolicySpecs = BuildIncludePolicySpecs();

    public static TheoryData<string> IncludePolicyCases => CaseIdsFrom(IncludePolicySpecs);

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

    [Theory(DisplayName = "GetAllIncludingDeletedAsync applies default include policy")]
    [MemberData(nameof(IncludePolicyCases))]
    public async Task GetAllIncludingDeletedAsync_DefaultIncludes_Policy_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = IncludePolicySpecs[caseId];
        var policy = spec.Mode is null
            ? new KyrolusRepositoryPolicy().SetDefaultIncludeProperties<Product>("Store")
            : new KyrolusRepositoryPolicy { DefaultIncludeMode = spec.Mode.Value }
                .SetDefaultIncludeProperties<Product>("Store");

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var items = await repo.GetAllIncludingDeletedAsync(
            filter: null,
            orderBy: null,
            includeProperties: spec.IncludeProperties?.ToList(),
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: true,
            cancellationToken: default);

        items.Count.ShouldBe(3);
        if (spec.ExpectStore is not null)
            items.All(p => (p.Store is not null) == spec.ExpectStore.Value).ShouldBeTrue();
        if (spec.ExpectReviews is not null)
            items.All(p => (p.Reviews is not null) == spec.ExpectReviews.Value).ShouldBeTrue();
    }

    private static IReadOnlyDictionary<string, IncludePolicySpec> BuildIncludePolicySpecs()
        => new Dictionary<string, IncludePolicySpec>
        {
            ["default"] = new IncludePolicySpec(
                null,
                null,
                ExpectStore: true,
                ExpectReviews: null),
            ["merge"] = new IncludePolicySpec(
                KyrolusDefaultIncludeMode.Merge,
                [nameof(Product.Reviews)],
                ExpectStore: true,
                ExpectReviews: true),
            ["replace"] = new IncludePolicySpec(
                KyrolusDefaultIncludeMode.Replace,
                [nameof(Product.Reviews)],
                ExpectStore: false,
                ExpectReviews: true)
        };

    // CaseIdsFrom is defined in GetAllIncludingDeletedAsyncTests.Helpers.cs
}
