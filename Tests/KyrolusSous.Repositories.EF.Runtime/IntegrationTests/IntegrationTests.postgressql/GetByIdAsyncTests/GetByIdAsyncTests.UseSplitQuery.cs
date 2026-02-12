namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    private sealed record SplitQuerySpec(
        bool? UseSplitQuery,
        KyrolusRepositoryPolicy? Policy,
        int ExpectedCommands,
        string Label);

    private static readonly IReadOnlyDictionary<string, SplitQuerySpec> SplitQuerySpecs = BuildSplitQuerySpecs();

    public static TheoryData<string> SplitQueryCases => CaseIdsFrom(SplitQuerySpecs);

    [Theory(DisplayName = "GetByIdAsync respects UseSplitQuery settings")]
    [MemberData(nameof(SplitQueryCases))]
    public async Task GetByIdAsync_UseSplitQuery_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SplitQuerySpecs[caseId];
        var customFactory = spec.Policy is null ? Factory : WithPolicy(spec.Policy);

        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();
        var item = await repo.GetByIdAsync(
            Guid.Parse(productLaptopId),
            includeProperties: ["Reviews", "OrderLines", "ProductCategories"],
            includeGraph: null,
            asNoTracking: true,
            useSplitQuery: spec.UseSplitQuery,
            cancellationToken: default);

        counter.Count.ShouldBe(spec.ExpectedCommands, $"Expected {spec.ExpectedCommands} SQL commands for {spec.Label}, got {counter.Count}");
        item.ShouldNotBeNull();
    }

    private static IReadOnlyDictionary<string, SplitQuerySpec> BuildSplitQuerySpecs()
        => new Dictionary<string, SplitQuerySpec>
        {
            ["true"] = new SplitQuerySpec(
                UseSplitQuery: true,
                Policy: null,
                ExpectedCommands: 4,
                Label: "UseSplitQuery=true"),
            ["false"] = new SplitQuerySpec(
                UseSplitQuery: false,
                Policy: null,
                ExpectedCommands: 1,
                Label: "UseSplitQuery=false"),
            ["policy-true"] = new SplitQuerySpec(
                UseSplitQuery: null,
                Policy: new KyrolusRepositoryPolicy { UseSplitQueryDefault = true },
                ExpectedCommands: 4,
                Label: "UseSplitQuery=null with policy true"),
            ["policy-false"] = new SplitQuerySpec(
                UseSplitQuery: null,
                Policy: new KyrolusRepositoryPolicy { UseSplitQueryDefault = false },
                ExpectedCommands: 1,
                Label: "UseSplitQuery=null with policy false")
        };
}
