namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdCompiledAsyncTests;

public partial class GetByIdCompiledAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private sealed record BasicSpec(Guid Id, bool ExpectFound, Action<Product>? Assert);

    private static readonly IReadOnlyDictionary<string, BasicSpec> BasicSpecs = BuildBasicSpecs();

    public static TheoryData<string> BasicCases => CaseIdsFrom(BasicSpecs);

    [Theory(DisplayName = "GetByIdCompiledAsync returns expected single-key entities")]
    [MemberData(nameof(BasicCases))]
    public async Task GetByIdCompiledAsync_Basic_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = BasicSpecs[caseId];

        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var item = await repo.GetByIdCompiledAsync(spec.Id);
        if (!spec.ExpectFound)
        {
            item.ShouldBeNull();
            return;
        }

        item.ShouldNotBeNull();
        spec.Assert?.Invoke(item!);
    }

    private static IReadOnlyDictionary<string, BasicSpec> BuildBasicSpecs()
        => new Dictionary<string, BasicSpec>
        {
            ["found-laptop"] = new(
                ExistingProductId,
                ExpectFound: true,
                Assert: item =>
                {
                    item.Id.ShouldBe(ExistingProductId);
                    item.Name.ShouldBe("Laptop Pro 15");
                }),
            ["found-headphones"] = new(
                DataSeeder.productHeadphonesId,
                ExpectFound: true,
                Assert: item =>
                {
                    item.Id.ShouldBe(DataSeeder.productHeadphonesId);
                    item.Price.ShouldBe(199m);
                }),
            ["missing"] = new(
                MissingProductId,
                ExpectFound: false,
                Assert: null)
        };
}
