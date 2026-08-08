namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    public static TheoryData<string, bool?, bool?, int> SplitQueryCases => new()
    {
        { "explicit-true", true, null, 4 },
        { "explicit-false", false, null, 1 },
        { "policy-true", null, true, 4 },
        { "policy-false", null, false, 1 },
        { "policy-null", null, null, 1 }
    };

    [Theory(DisplayName = "GetAllIncludingDeletedAsync respects UseSplitQuery resolution")]
    [MemberData(nameof(SplitQueryCases))]
    public async Task GetAllIncludingDeletedAsync_UseSplitQuery_Works(string caseId, bool? useSplitQuery, bool? policyDefault, int expectedCount)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var policy = new KyrolusRepositoryPolicy { UseSplitQueryDefault = policyDefault };
        var customFactory = WithPolicy(policy);
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
            useSplitQuery: useSplitQuery,
            cancellationToken: default);

        counter.Count.ShouldBe(expectedCount, $"Expected {expectedCount} SQL commands for case '{caseId}', got {counter.Count}");
        items.ShouldNotBeNull();
    }
}
