namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdCompiledAsyncTests;

public partial class GetByIdCompiledAsyncTests
{
    private sealed record SplitQuerySpec(bool? PolicyUseSplitQueryDefault, int ExpectedCommandCount);

    private static readonly IReadOnlyDictionary<string, SplitQuerySpec> SplitQuerySpecs = BuildSplitQuerySpecs();

    public static TheoryData<string> SplitQueryCases => CaseIdsFrom(SplitQuerySpecs);

    [Theory(DisplayName = "GetByIdCompiledAsync respects UseSplitQuery policy defaults")]
    [MemberData(nameof(SplitQueryCases))]
    public async Task GetByIdCompiledAsync_UseSplitQuery_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SplitQuerySpecs[caseId];

        var policy = new KyrolusRepositoryPolicy
        {
            UseSplitQueryDefault = spec.PolicyUseSplitQueryDefault
        }.SetDefaultIncludeProperties<Product>("Reviews", "OrderLines", "ProductCategories");

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        counter.Reset();

        var item = await repo.GetByIdCompiledAsync(ExistingProductId);
        item.ShouldNotBeNull();

        counter.Count.ShouldBe(spec.ExpectedCommandCount,
            $"Expected {spec.ExpectedCommandCount} SQL commands for case '{caseId}', got {counter.Count}.");
    }

    private static IReadOnlyDictionary<string, SplitQuerySpec> BuildSplitQuerySpecs()
        => new Dictionary<string, SplitQuerySpec>
        {
            ["policy-null-default-false"] = new(
                PolicyUseSplitQueryDefault: null,
                ExpectedCommandCount: 1),
            ["policy-false"] = new(
                PolicyUseSplitQueryDefault: false,
                ExpectedCommandCount: 1),
            ["policy-true"] = new(
                PolicyUseSplitQueryDefault: true,
                ExpectedCommandCount: 4)
        };
}
