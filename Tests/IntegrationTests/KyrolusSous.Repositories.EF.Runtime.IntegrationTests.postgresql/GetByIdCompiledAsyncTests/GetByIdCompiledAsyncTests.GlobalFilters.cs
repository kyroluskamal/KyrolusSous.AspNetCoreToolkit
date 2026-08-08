namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdCompiledAsyncTests;

public partial class GetByIdCompiledAsyncTests
{
    private sealed record GlobalFilterSpec(KyrolusRepositoryPolicy Policy, Guid Id, bool ExpectFound);

    private static readonly IReadOnlyDictionary<string, GlobalFilterSpec> GlobalFilterSpecs = BuildGlobalFilterSpecs();

    public static TheoryData<string> GlobalFilterCases => CaseIdsFrom(GlobalFilterSpecs);

    [Theory(DisplayName = "GetByIdCompiledAsync respects global filters")]
    [MemberData(nameof(GlobalFilterCases))]
    public async Task GetByIdCompiledAsync_GlobalFilter_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = GlobalFilterSpecs[caseId];

        var customFactory = WithPolicy(spec.Policy);
        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var item = await repo.GetByIdCompiledAsync(spec.Id);
        if (spec.ExpectFound)
            item.ShouldNotBeNull();
        else
            item.ShouldBeNull();
    }

    private static IReadOnlyDictionary<string, GlobalFilterSpec> BuildGlobalFilterSpecs()
        => new Dictionary<string, GlobalFilterSpec>
        {
            ["blocked-by-filter"] = new(
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(p => p.Price >= 1250m),
                Id: ExistingProductId,
                ExpectFound: false),
            ["allowed-by-filter"] = new(
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(p => p.Price >= 1000m),
                Id: ExistingProductId,
                ExpectFound: true)
        };
}
