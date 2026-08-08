namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdIncludingDeletedAsyncTests;

public partial class GetByIdIncludingDeletedAsyncTests
{
    private sealed record TrackingSpec(bool IsComposite, bool? AsNoTracking, bool? PolicyAsNoTrackingDefault, bool ExpectTracked);

    private static readonly IReadOnlyDictionary<string, TrackingSpec> TrackingSpecs = BuildTrackingSpecs();

    public static TheoryData<string> TrackingCases => CaseIdsFrom(TrackingSpecs);

    [Theory(DisplayName = "GetByIdIncludingDeletedAsync respects AsNoTracking settings")]
    [MemberData(nameof(TrackingCases))]
    public async Task GetByIdIncludingDeletedAsync_AsNoTracking_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = TrackingSpecs[caseId];
        var policy = spec.PolicyAsNoTrackingDefault is null
            ? null
            : new KyrolusRepositoryPolicy { AsNoTrackingDefault = spec.PolicyAsNoTrackingDefault };
        var customFactory = policy is null ? Factory : WithPolicy(policy);

        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.ChangeTracker.Clear();

        if (spec.IsComposite)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            _ = await repo.GetByIdIncludingDeletedAsync(
                ExistingReviewKey,
                includeProperties: null,
                includeGraph: null,
                asNoTracking: spec.AsNoTracking,
                useSplitQuery: null,
                cancellationToken: default);
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            _ = await repo.GetByIdIncludingDeletedAsync(
                ExistingProductId,
                includeProperties: null,
                includeGraph: null,
                asNoTracking: spec.AsNoTracking,
                useSplitQuery: null,
                cancellationToken: default);
        }

        if (spec.ExpectTracked)
            dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
        else
            dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }

    private static IReadOnlyDictionary<string, TrackingSpec> BuildTrackingSpecs()
        => new Dictionary<string, TrackingSpec>
        {
            ["single-explicit-true"] = new(false, true, null, false),
            ["single-explicit-false"] = new(false, false, null, true),
            ["single-policy-true"] = new(false, null, true, false),
            ["single-policy-false"] = new(false, null, false, true),
            ["single-policy-null"] = new(false, null, null, false),
            ["composite-explicit-true"] = new(true, true, null, false),
            ["composite-explicit-false"] = new(true, false, null, true)
        };
}
