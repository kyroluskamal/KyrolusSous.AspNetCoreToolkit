namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdIncludingDeletedAsyncTests;

public partial class GetByIdIncludingDeletedAsyncTests
{
    private sealed record GlobalFilterSpec(bool IsComposite, KyrolusRepositoryPolicy Policy, bool ExpectFound);

    private static readonly IReadOnlyDictionary<string, GlobalFilterSpec> GlobalFilterSpecs = BuildGlobalFilterSpecs();

    public static TheoryData<string> GlobalFilterCases => CaseIdsFrom(GlobalFilterSpecs);

    [Theory(DisplayName = "GetByIdIncludingDeletedAsync respects global filters")]
    [MemberData(nameof(GlobalFilterCases))]
    public async Task GetByIdIncludingDeletedAsync_GlobalFilter_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = GlobalFilterSpecs[caseId];
        var customFactory = WithPolicy(spec.Policy);
        using var scope = customFactory.Services.CreateScope();

        if (spec.IsComposite)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var item = await repo.GetByIdIncludingDeletedAsync(ExistingReviewKey);
            if (spec.ExpectFound)
                item.ShouldNotBeNull();
            else
                item.ShouldBeNull();
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var single = await singleRepo.GetByIdIncludingDeletedAsync(ExistingProductId);
        if (spec.ExpectFound)
            single.ShouldNotBeNull();
        else
            single.ShouldBeNull();
    }

    private static IReadOnlyDictionary<string, GlobalFilterSpec> BuildGlobalFilterSpecs()
        => new Dictionary<string, GlobalFilterSpec>
        {
            ["single-blocked"] = new(
                IsComposite: false,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(p => p.Price >= 1250m),
                ExpectFound: false),
            ["single-allowed"] = new(
                IsComposite: false,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(p => p.Price >= 1000m),
                ExpectFound: true),
            ["composite-blocked"] = new(
                IsComposite: true,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Review>(r => r.Rating < 5),
                ExpectFound: false),
            ["composite-allowed"] = new(
                IsComposite: true,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Review>(r => r.Rating <= 5),
                ExpectFound: true)
        };
}
