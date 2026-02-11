namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    public sealed record SplitQueryCase(
        string CaseId,
        bool? UseSplitQuery,
        bool? PolicyDefault,
        int ExpectedCount);

    public static TheoryData<SplitQueryCase> SplitQueryCases => new()
    {
        new SplitQueryCase("explicit-true", true, null, 4),
        new SplitQueryCase("explicit-false", false, null, 1),
        new SplitQueryCase("policy-true", null, true, 4),
        new SplitQueryCase("policy-false", null, false, 1),
        new SplitQueryCase("policy-null", null, null, 1)
    };

    [Theory(DisplayName = "GetAllIncludingDeletedAsync respects UseSplitQuery resolution")]
    [MemberData(nameof(SplitQueryCases))]
    public async Task GetAllIncludingDeletedAsync_UseSplitQuery_Works(SplitQueryCase testCase)
    {
        var policy = new KyrolusRepositoryPolicy { UseSplitQueryDefault = testCase.PolicyDefault };
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
            useSplitQuery: testCase.UseSplitQuery,
            cancellationToken: default);

        counter.Count.ShouldBe(testCase.ExpectedCount, $"Expected {testCase.ExpectedCount} SQL commands for case '{testCase.CaseId}', got {counter.Count}");
        items.ShouldNotBeNull();
    }
}
