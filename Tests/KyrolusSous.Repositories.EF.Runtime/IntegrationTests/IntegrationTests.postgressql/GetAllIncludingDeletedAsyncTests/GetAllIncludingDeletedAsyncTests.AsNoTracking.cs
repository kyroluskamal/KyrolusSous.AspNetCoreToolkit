namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    private enum EntityKind
    {
        Product,
        Review
    }

    private sealed record AsNoTrackingSpec(EntityKind Kind, bool? Input, bool? Policy, bool Expected);

    private static readonly IReadOnlyDictionary<string, AsNoTrackingSpec> AsNoTrackingSpecs = BuildAsNoTrackingSpecs();

    public static TheoryData<string> AsNoTrackingCases => CaseIdsFrom(AsNoTrackingSpecs);

    [Theory(DisplayName = "GetAllIncludingDeletedAsync respects AsNoTracking resolution (input > policy > default)")]
    [MemberData(nameof(AsNoTrackingCases))]
    public Task GetAllIncludingDeletedAsync_AsNoTracking_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = AsNoTrackingSpecs[caseId];
        return spec.Kind == EntityKind.Product
            ? AssertAsNoTrackingForProduct(spec)
            : AssertAsNoTrackingForReview(spec);
    }

    private Task AssertAsNoTrackingForProduct(AsNoTrackingSpec spec)
    {
        var finalPolicy = spec.Policy is not null ? new KyrolusRepositoryPolicy { AsNoTrackingDefault = spec.Policy } : null;
        var queryRequest = new QueryRequest(AsNoTracking: spec.Input);
        return WithProductSoftDeleted(async (repo, sp) =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            db.ChangeTracker.Clear();
            await repo.GetAllIncludingDeletedAsync(asNoTracking: spec.Input);
            AssertAsNoTracking(spec.Expected, db);
        }, queryRequest, finalPolicy);
    }

    private Task AssertAsNoTrackingForReview(AsNoTrackingSpec spec)
    {
        var finalPolicy = spec.Policy is not null ? new KyrolusRepositoryPolicy { AsNoTrackingDefault = spec.Policy } : null;
        var queryRequest = new QueryRequest(AsNoTracking: spec.Input);
        return WithReviewSoftDeleted(async (repo, sp) =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            db.ChangeTracker.Clear();
            await repo.GetAllIncludingDeletedAsync(asNoTracking: spec.Input);
            AssertAsNoTracking(spec.Expected, db);
        }, queryRequest, finalPolicy);
    }

    private static IReadOnlyDictionary<string, AsNoTrackingSpec> BuildAsNoTrackingSpecs()
    {
        var data = new Dictionary<string, AsNoTrackingSpec>();
        (bool? input, bool? policy, bool expected)[] baseCases =
        [
            (true,  null,  true),
            (false, true,  false),
            (null,  true,  true),
            (null,  false, false),
            (null,  null,  true),
        ];

        foreach (var (input, policy, expected) in baseCases)
        {
            var suffix = $"input:{input}-policy:{policy}";
            data[$"product-{suffix}"] = new AsNoTrackingSpec(EntityKind.Product, input, policy, expected);
            data[$"review-{suffix}"] = new AsNoTrackingSpec(EntityKind.Review, input, policy, expected);
        }

        return data;
    }

    private static void AssertAsNoTracking(bool expected, ApplicationDbContext dbContext)
    {
        if (expected)
            dbContext.ChangeTracker.Entries().ShouldBeEmpty();
        else
            dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }

    // CaseIdsFrom is defined in GetAllIncludingDeletedAsyncTests.Helpers.cs
}
