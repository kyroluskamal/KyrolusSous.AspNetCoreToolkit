namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    private sealed record GlobalFilterSpec(Func<GetByIdAsyncTests, Task> Run);

    private static readonly IReadOnlyDictionary<string, GlobalFilterSpec> GlobalFilterSpecs = BuildGlobalFilterSpecs();

    public static TheoryData<string> GlobalFilterCases => CaseIdsFrom(GlobalFilterSpecs);

    [Theory(DisplayName = "GetByIdAsync respects global filters")]
    [MemberData(nameof(GlobalFilterCases))]
    public Task GetByIdAsync_GlobalFilter_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return GlobalFilterSpecs[caseId].Run(this);
    }

    private static IReadOnlyDictionary<string, GlobalFilterSpec> BuildGlobalFilterSpecs()
        => new Dictionary<string, GlobalFilterSpec>
        {
            ["single"] = new GlobalFilterSpec(async test =>
            {
                var policy = new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(p => p.Price >= 1250m);
                var customFactory = test.WithPolicy(policy);
                using var scope = customFactory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

                var item = await repo.GetByIdAsync(Guid.Parse(productLaptopId));
                item.ShouldBeNull();
            }),
            ["composite"] = new GlobalFilterSpec(async test =>
            {
                var policy = new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Review>(r => r.Rating < 5);
                var customFactory = test.WithPolicy(policy);
                using var scope = customFactory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, Review>>();

                var item = await repo.GetByIdAsync(CompositeKey_ProductReview);
                item.ShouldBeNull();
            })
        };
}
