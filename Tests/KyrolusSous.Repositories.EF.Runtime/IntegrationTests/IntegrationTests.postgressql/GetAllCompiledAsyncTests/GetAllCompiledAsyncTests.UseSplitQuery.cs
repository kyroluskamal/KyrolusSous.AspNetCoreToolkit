namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllCompiledAsyncTests;

public partial class GetAllCompiledAsyncTests
{
    private sealed record SplitQuerySpec(
        bool? UseSplitQuery,
        bool? PolicyDefault,
        int ExpectedCommandCount);

    private static readonly IReadOnlyDictionary<string, SplitQuerySpec> SplitQuerySpecs = BuildSplitQuerySpecs();

    public static TheoryData<string> SplitQueryCases => CaseIdsFrom(SplitQuerySpecs);

    [Theory(DisplayName = "GetAllCompiledAsync respects UseSplitQuery resolution")]
    [MemberData(nameof(SplitQueryCases))]
    public async Task GetAllCompiledAsync_UseSplitQuery_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SplitQuerySpecs[caseId];
        var policy = new KyrolusRepositoryPolicy
        {
            UseSplitQueryDefault = spec.PolicyDefault
        }.SetDefaultIncludeProperties<Product>("Reviews", "OrderLines", "ProductCategories");

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var items = await repo.GetAllCompiledAsync(
            p => p.Price > 0m,
            asNoTracking: true,
            useSplitQuery: spec.UseSplitQuery,
            cancellationToken: default);

        items.Count.ShouldBe(3);
        counter.Count.ShouldBe(spec.ExpectedCommandCount,
            $"Expected {spec.ExpectedCommandCount} SQL commands for case '{caseId}', got {counter.Count}.");
    }

    private static IReadOnlyDictionary<string, SplitQuerySpec> BuildSplitQuerySpecs()
        => new Dictionary<string, SplitQuerySpec>
        {
            ["explicit-true"] = new(true, null, 4),
            ["explicit-false"] = new(false, null, 1),
            ["policy-true"] = new(null, true, 4),
            ["policy-false"] = new(null, false, 1),
            ["policy-null"] = new(null, null, 1)
        };
}
