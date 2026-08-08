namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdIncludingDeletedAsyncTests;

public partial class GetByIdIncludingDeletedAsyncTests
{
    private sealed record SplitQuerySpec(bool IsComposite, bool? UseSplitQuery, bool? PolicyUseSplitQueryDefault, int ExpectedCommands);

    private static readonly IReadOnlyDictionary<string, SplitQuerySpec> SplitQuerySpecs = BuildSplitQuerySpecs();

    public static TheoryData<string> SplitQueryCases => CaseIdsFrom(SplitQuerySpecs);

    [Theory(DisplayName = "GetByIdIncludingDeletedAsync respects UseSplitQuery settings")]
    [MemberData(nameof(SplitQueryCases))]
    public async Task GetByIdIncludingDeletedAsync_UseSplitQuery_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SplitQuerySpecs[caseId];
        var policy = new KyrolusRepositoryPolicy
        {
            UseSplitQueryDefault = spec.PolicyUseSplitQueryDefault
        };
        if (spec.IsComposite)
            policy = policy.SetDefaultIncludeProperties<Review>("Product", "Customer");
        else
            policy = policy.SetDefaultIncludeProperties<Product>("Reviews", "OrderLines", "ProductCategories");

        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var counter = scope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();
        counter.Reset();

        if (spec.IsComposite)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var item = await repo.GetByIdIncludingDeletedAsync(
                ExistingReviewKey,
                includeProperties: null,
                includeGraph: null,
                asNoTracking: true,
                useSplitQuery: spec.UseSplitQuery,
                cancellationToken: default);
            item.ShouldNotBeNull();
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var item = await repo.GetByIdIncludingDeletedAsync(
                ExistingProductId,
                includeProperties: null,
                includeGraph: null,
                asNoTracking: true,
                useSplitQuery: spec.UseSplitQuery,
                cancellationToken: default);
            item.ShouldNotBeNull();
        }

        counter.Count.ShouldBe(spec.ExpectedCommands, $"Expected {spec.ExpectedCommands} SQL commands for case '{caseId}', got {counter.Count}.");
    }

    private static IReadOnlyDictionary<string, SplitQuerySpec> BuildSplitQuerySpecs()
        => new Dictionary<string, SplitQuerySpec>
        {
            ["single-explicit-true"] = new(false, true, null, 4),
            ["single-explicit-false"] = new(false, false, null, 1),
            ["single-policy-true"] = new(false, null, true, 4),
            ["single-policy-false"] = new(false, null, false, 1),
            ["single-policy-null"] = new(false, null, null, 1),
            ["composite-explicit-true"] = new(true, true, null, 1),
            ["composite-explicit-false"] = new(true, false, null, 1),
            ["composite-policy-true"] = new(true, null, true, 1)
        };
}
