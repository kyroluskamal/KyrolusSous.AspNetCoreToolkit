namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    public static TheoryData<string, bool?, bool?, bool> AsNoTrackingCases => new()
    {
        { "explicit-true", true, null, true },
        { "explicit-false", false, null, false },
        { "policy-true", null, true, true },
        { "policy-false", null, false, false },
        { "policy-null", null, null, true }
    };

    [Theory(DisplayName = "GetAllAsync respects AsNoTracking resolution")]
    [MemberData(nameof(AsNoTrackingCases))]
    public async Task GetAllAsync_AsNoTracking_Works(string caseId, bool? input, bool? policyDefault, bool expectNoTracking)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var policy = new KyrolusRepositoryPolicy { AsNoTrackingDefault = policyDefault };
        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        dbContext.ChangeTracker.Clear();

        await repo.GetAllAsync(
            filter: null,
            orderBy: null,
            includeProperties: null,
            includeGraph: null,
            asNoTracking: input,
            useSplitQuery: null,
            cancellationToken: default);

        if (expectNoTracking)
            dbContext.ChangeTracker.Entries().ShouldBeEmpty();
        else
            dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }
}
