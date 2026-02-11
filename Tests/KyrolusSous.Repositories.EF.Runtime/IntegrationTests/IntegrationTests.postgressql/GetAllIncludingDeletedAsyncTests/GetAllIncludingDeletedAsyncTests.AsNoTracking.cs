namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    public static TheoryData<KeyType, bool?, bool?, bool> CasesByKeyType
    {
        get
        {
            var data = new TheoryData<KeyType, bool?, bool?, bool>();

            (bool? input, bool? policy, bool expected)[] baseCases =
            [
                (true,  null,  true),
                (false, true,  false),
                (null,  true,  true),
                (null,  false, false),
                (null,  null,  true),
            ];

            foreach (var keyType in new[] { KeyType.Single, KeyType.Composite })
                foreach (var (input, policy, expected) in baseCases)
                    data.Add(keyType, input, policy, expected);
            return data;
        }
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync respects AsNoTracking resolution (input > policy > default) -- SingleKey")]
    [MemberData(nameof(CasesByKeyType))]
    public async Task GetAllIncludingDeletedAsync_AsNoTracking_Works_SingleKey(KeyType keyType, bool? input, bool? policy, bool expected)
    {
        var finalPolicy = policy is not null ? new KyrolusRepositoryPolicy { AsNoTrackingDefault = policy } : null;
        var queryRequest = new QueryRequest(AsNoTracking: input);
        if (keyType == KeyType.Single)
            await WithSoftDeletedAsync_SingleKey<Product>(DataSeeder.productLaptopId, async (_, products, _, repo, sp) =>
            {
                var db = sp?.GetRequiredService<ApplicationDbContext>()!;
                db.ChangeTracker.Clear();
                await repo.GetAllIncludingDeletedAsync(asNoTracking: input);
                AssertAsNoTracking(expected, sp?.GetRequiredService<ApplicationDbContext>()!);
            }, queryRequest, finalPolicy);
        else
            await WithSoftDeletedAsync_CompositeKey<Review>(DataSeeder.ReviewLapTopKey, async (_, products, _, repo, sp) =>
            {
                var db = sp?.GetRequiredService<ApplicationDbContext>()!;
                db.ChangeTracker.Clear();
                await repo.GetAllIncludingDeletedAsync(asNoTracking: input);
                AssertAsNoTracking(expected, sp?.GetRequiredService<ApplicationDbContext>()!);
            }, queryRequest, finalPolicy);
    }

    private static void AssertAsNoTracking(bool expected, ApplicationDbContext dbContext)
    {
        if (expected)
            dbContext.ChangeTracker.Entries().ShouldBeEmpty();
        else
            dbContext.ChangeTracker.Entries().ShouldNotBeEmpty();
    }
}
