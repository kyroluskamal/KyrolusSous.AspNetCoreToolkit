namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.QueryAsyncTests;

public partial class QueryAsyncTests
{
    public static TheoryData<string, bool> AsNoTrackingPolicyCases => new()
    {
        { "policy-asnotracking-true", true },
        { "policy-asnotracking-false", false }
    };

    [Theory(DisplayName = "QueryAsync overload uses policy AsNoTracking default when parameter is null")]
    [MemberData(nameof(AsNoTrackingPolicyCases))]
    public async Task QueryAsync_Overload_UsesPolicyDefault_AsNoTracking(string caseId, bool policyAsNoTrackingDefault)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var policy = new KyrolusRepositoryPolicy { AsNoTrackingDefault = policyAsNoTrackingDefault };
        var customFactory = WithPolicy(policy);
        using var scope = customFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        db.ChangeTracker.Clear();
        _ = await repo.QueryAsync(
            x => x.Id == DataSeeder.productLaptopId,
            x => x,
            asNoTracking: null,
            useSplitQuery: false);

        if (policyAsNoTrackingDefault)
            db.ChangeTracker.Entries().ShouldBeEmpty();
        else
            db.ChangeTracker.Entries().ShouldNotBeEmpty();
    }

    [Fact(DisplayName = "QueryAsync overload uses policy split-query default when parameter is null")]
    public async Task QueryAsync_Overload_UsesPolicyDefault_UseSplitQuery()
    {
        var nonSplitFactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = false });
        using var nonSplitScope = nonSplitFactory.Services.CreateScope();
        var nonSplitRepo = nonSplitScope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var nonSplitCounter = nonSplitScope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        nonSplitCounter.Reset();
        var nonSplitItems = await nonSplitRepo.QueryAsync(
            x => x.Id == DataSeeder.productLaptopId,
            x => x,
            asNoTracking: true,
            useSplitQuery: null,
            includeExpressions:
            [
                x => x.Reviews,
                x => x.OrderLines,
                x => x.ProductCategories
            ]);
        nonSplitItems.Count.ShouldBe(1);
        var nonSplitCommands = nonSplitCounter.Count;

        var splitFactory = WithPolicy(new KyrolusRepositoryPolicy { UseSplitQueryDefault = true });
        using var splitScope = splitFactory.Services.CreateScope();
        var splitRepo = splitScope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var splitCounter = splitScope.ServiceProvider.GetRequiredService<CommandCounterInterceptor>();

        splitCounter.Reset();
        var splitItems = await splitRepo.QueryAsync(
            x => x.Id == DataSeeder.productLaptopId,
            x => x,
            asNoTracking: true,
            useSplitQuery: null,
            includeExpressions:
            [
                x => x.Reviews,
                x => x.OrderLines,
                x => x.ProductCategories
            ]);
        splitItems.Count.ShouldBe(1);
        var splitCommands = splitCounter.Count;

        nonSplitCommands.ShouldBeGreaterThan(0);
        splitCommands.ShouldBeGreaterThan(nonSplitCommands);
    }
}
