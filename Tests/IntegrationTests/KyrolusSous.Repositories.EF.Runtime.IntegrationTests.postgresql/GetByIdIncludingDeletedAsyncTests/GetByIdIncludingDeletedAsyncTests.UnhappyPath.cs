namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdIncludingDeletedAsyncTests;

public partial class GetByIdIncludingDeletedAsyncTests
{
    private sealed record UnhappySpec(Func<GetByIdIncludingDeletedAsyncTests, Task> Run);

    private static readonly IReadOnlyDictionary<string, UnhappySpec> UnhappySpecs = BuildUnhappySpecs();

    public static TheoryData<string> UnhappyCases => CaseIdsFrom(UnhappySpecs);

    [Theory(DisplayName = "GetByIdIncludingDeletedAsync handles invalid inputs")]
    [MemberData(nameof(UnhappyCases))]
    public Task GetByIdIncludingDeletedAsync_UnhappyPath_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return UnhappySpecs[caseId].Run(this);
    }

    private static IReadOnlyDictionary<string, UnhappySpec> BuildUnhappySpecs()
        => new Dictionary<string, UnhappySpec>
        {
            ["invalid-include-single"] = new(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

                await Should.ThrowAsync<InvalidOperationException>(async () =>
                {
                    await repo.GetByIdIncludingDeletedAsync(
                        ExistingProductId,
                        includeProperties: ["NotARealNavigation"],
                        includeGraph: null,
                        asNoTracking: true,
                        useSplitQuery: true,
                        cancellationToken: default);
                });
            }),
            ["invalid-include-composite"] = new(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

                await Should.ThrowAsync<InvalidOperationException>(async () =>
                {
                    await repo.GetByIdIncludingDeletedAsync(
                        ExistingReviewKey,
                        includeProperties: ["NotARealNavigation"],
                        includeGraph: null,
                        asNoTracking: true,
                        useSplitQuery: true,
                        cancellationToken: default);
                });
            }),
            ["invalid-composite-length"] = new(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

                await Should.ThrowAsync<ArgumentException>(async () =>
                {
                    await repo.GetByIdIncludingDeletedAsync([DataSeeder.productLaptopId]);
                });
            }),
            ["null-composite-keys"] = new(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

                await Should.ThrowAsync<ArgumentException>(async () =>
                {
                    await repo.GetByIdIncludingDeletedAsync(null!);
                });
            }),
            ["composite-order-matters"] = new(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var item = await repo.GetByIdIncludingDeletedAsync(ExistingReviewKeyReversed);
                item.ShouldBeNull();
            })
        };
}
