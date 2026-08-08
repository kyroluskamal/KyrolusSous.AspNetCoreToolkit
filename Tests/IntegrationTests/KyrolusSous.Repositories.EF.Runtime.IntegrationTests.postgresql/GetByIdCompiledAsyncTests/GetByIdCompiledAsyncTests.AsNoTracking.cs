namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdCompiledAsyncTests;

public partial class GetByIdCompiledAsyncTests
{
    private sealed record TrackingSpec(bool? PolicyAsNoTrackingDefault, bool ExpectTracked);

    private static readonly IReadOnlyDictionary<string, TrackingSpec> TrackingSpecs = BuildTrackingSpecs();

    public static TheoryData<string> TrackingCases => CaseIdsFrom(TrackingSpecs);

    [Theory(DisplayName = "GetByIdCompiledAsync respects AsNoTracking policy defaults")]
    [MemberData(nameof(TrackingCases))]
    public async Task GetByIdCompiledAsync_AsNoTracking_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = TrackingSpecs[caseId];

        var policy = spec.PolicyAsNoTrackingDefault is null
            ? null
            : new KyrolusRepositoryPolicy { AsNoTrackingDefault = spec.PolicyAsNoTrackingDefault };
        var customFactory = policy is null ? Factory : WithPolicy(policy);

        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        dbContext.ChangeTracker.Clear();

        var item = await repo.GetByIdCompiledAsync(ExistingProductId);
        item.ShouldNotBeNull();

        if (spec.ExpectTracked)
            dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
        else
            dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }

    private static IReadOnlyDictionary<string, TrackingSpec> BuildTrackingSpecs()
        => new Dictionary<string, TrackingSpec>
        {
            ["policy-null-default-true"] = new(
                PolicyAsNoTrackingDefault: null,
                ExpectTracked: false),
            ["policy-true"] = new(
                PolicyAsNoTrackingDefault: true,
                ExpectTracked: false),
            ["policy-false"] = new(
                PolicyAsNoTrackingDefault: false,
                ExpectTracked: true)
        };
}
