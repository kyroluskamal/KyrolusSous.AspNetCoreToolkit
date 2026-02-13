namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllCompiledAsyncTests;

public partial class GetAllCompiledAsyncTests
{
    public static TheoryData<string, bool?, bool?, bool> AsNoTrackingCases => new()
    {
        { "explicit-true", true, null, true },
        { "explicit-false", false, null, false },
        { "policy-true", null, true, true },
        { "policy-false", null, false, false },
        { "policy-null", null, null, true }
    };

    [Theory(DisplayName = "GetAllCompiledAsync respects AsNoTracking resolution")]
    [MemberData(nameof(AsNoTrackingCases))]
    public async Task GetAllCompiledAsync_AsNoTracking_Works(string caseId, bool? input, bool? policyDefault, bool expectNoTracking)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var policy = new KyrolusRepositoryPolicy { AsNoTrackingDefault = policyDefault };
        var customFactory = WithPolicy(policy);

        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        dbContext.ChangeTracker.Clear();

        var items = await repo.GetAllCompiledAsync(
            p => p.Price > 0m,
            asNoTracking: input,
            useSplitQuery: null,
            cancellationToken: default);

        items.Count.ShouldBe(3);

        if (expectNoTracking)
            dbContext.ChangeTracker.Entries().ShouldBeEmpty();
        else
            dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }
}
