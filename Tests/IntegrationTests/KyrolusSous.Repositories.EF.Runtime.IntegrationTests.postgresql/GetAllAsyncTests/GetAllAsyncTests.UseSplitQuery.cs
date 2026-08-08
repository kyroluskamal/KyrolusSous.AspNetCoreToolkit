namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    private static readonly IncludeGraph<Product> ReviewsGraph = new(x => x.Reviews);

    private sealed record SplitQuerySpec(
        bool? UseSplitQuery,
        bool? PolicyDefault,
        string[]? IncludeProperties,
        IncludeGraph<Product>? IncludeGraph,
        int ExpectedCount);

    private static readonly IReadOnlyDictionary<string, SplitQuerySpec> SplitQuerySpecs = BuildSplitQuerySpecs();

    public static TheoryData<string> SplitQueryCases => CaseIdsFrom(SplitQuerySpecs);

    [Theory(DisplayName = "GetAllAsync respects UseSplitQuery resolution")]
    [MemberData(nameof(SplitQueryCases))]
    public async Task GetAllAsync_UseSplitQuery_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SplitQuerySpecs[caseId];
        var policy = new KyrolusRepositoryPolicy { UseSplitQueryDefault = spec.PolicyDefault };
        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: spec.IncludeProperties?.ToList(),
            includeGraph: spec.IncludeGraph,
            asNoTracking: true,
            useSplitQuery: spec.UseSplitQuery,
            cancellationToken: default);

        counter.Count.ShouldBe(spec.ExpectedCount, $"Expected {spec.ExpectedCount} SQL commands for case '{caseId}', got {counter.Count}");
        items.ShouldNotBeNull();
    }

    private static IReadOnlyDictionary<string, SplitQuerySpec> BuildSplitQuerySpecs()
        => new Dictionary<string, SplitQuerySpec>
        {
            ["explicit-true-multi"] = new SplitQuerySpec(true, null,
                ["Reviews", "OrderLines", "ProductCategories"], null, 4),
            ["explicit-false-multi"] = new SplitQuerySpec(false, null,
                ["Reviews", "OrderLines", "ProductCategories"], null, 1),
            ["policy-true-multi"] = new SplitQuerySpec(null, true,
                ["Reviews", "OrderLines", "ProductCategories"], null, 4),
            ["policy-false-multi"] = new SplitQuerySpec(null, false,
                ["Reviews", "OrderLines", "ProductCategories"], null, 1),
            ["policy-null-multi"] = new SplitQuerySpec(null, null,
                ["Reviews", "OrderLines", "ProductCategories"], null, 1),
            ["explicit-true-single-collection"] = new SplitQuerySpec(true, null,
                ["Reviews"], null, 2),
            ["explicit-true-no-includes"] = new SplitQuerySpec(true, null,
                null, null, 1),
            ["explicit-true-reference-only"] = new SplitQuerySpec(true, null,
                ["Store"], null, 1),
            ["explicit-true-graph-collection"] = new SplitQuerySpec(true, null,
                ["Store"], ReviewsGraph, 2),
            ["explicit-false-graph-collection"] = new SplitQuerySpec(false, null,
                ["Store"], ReviewsGraph, 1)
        };

    // CaseIdsFrom is defined in GetAllAsyncTests.Helpers.cs
}
