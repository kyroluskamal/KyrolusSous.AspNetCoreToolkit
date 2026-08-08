namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    private sealed record TrackingSpec(
        bool IsComposite,
        bool? AsNoTracking,
        KyrolusRepositoryPolicy? Policy,
        bool ExpectTracked);

    private static readonly IReadOnlyDictionary<string, TrackingSpec> TrackingSpecs = BuildTrackingSpecs();

    public static TheoryData<string> TrackingCases => CaseIdsFrom(TrackingSpecs);

    [Theory(DisplayName = "GetByIdAsync respects AsNoTracking settings")]
    [MemberData(nameof(TrackingCases))]
    public async Task GetByIdAsync_AsNoTracking_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = TrackingSpecs[caseId];
        var customFactory = spec.Policy is null ? Factory : WithPolicy(spec.Policy);

        using var scope = customFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.ChangeTracker.Clear();

        if (spec.IsComposite)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, Review>>();
            await repo.GetByIdAsync(CompositeKey_ProductReview, asNoTracking: spec.AsNoTracking);
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            await repo.GetByIdAsync(Guid.Parse(productLaptopId), asNoTracking: spec.AsNoTracking);
        }

        if (spec.ExpectTracked)
            dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
        else
            dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }

    private static IReadOnlyDictionary<string, TrackingSpec> BuildTrackingSpecs()
        => new Dictionary<string, TrackingSpec>
        {
            ["single-true"] = new TrackingSpec(
                IsComposite: false,
                AsNoTracking: true,
                Policy: null,
                ExpectTracked: false),
            ["composite-true"] = new TrackingSpec(
                IsComposite: true,
                AsNoTracking: true,
                Policy: null,
                ExpectTracked: false),
            ["single-false"] = new TrackingSpec(
                IsComposite: false,
                AsNoTracking: false,
                Policy: null,
                ExpectTracked: true),
            ["composite-false"] = new TrackingSpec(
                IsComposite: true,
                AsNoTracking: false,
                Policy: null,
                ExpectTracked: true),
            ["policy-true"] = new TrackingSpec(
                IsComposite: false,
                AsNoTracking: null,
                Policy: new KyrolusRepositoryPolicy { AsNoTrackingDefault = true },
                ExpectTracked: false),
            ["policy-false"] = new TrackingSpec(
                IsComposite: false,
                AsNoTracking: null,
                Policy: new KyrolusRepositoryPolicy { AsNoTrackingDefault = false },
                ExpectTracked: true)
        };
}
