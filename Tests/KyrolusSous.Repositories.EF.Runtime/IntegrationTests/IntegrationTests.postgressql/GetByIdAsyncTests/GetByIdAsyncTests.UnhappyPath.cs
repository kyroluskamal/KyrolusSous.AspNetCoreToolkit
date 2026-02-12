namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    private sealed record UnhappySpec(Func<GetByIdAsyncTests, Task> Run);

    private static readonly IReadOnlyDictionary<string, UnhappySpec> UnhappySpecs = BuildUnhappySpecs();

    public static TheoryData<string> UnhappyCases => CaseIdsFrom(UnhappySpecs);

    [Theory(DisplayName = "GetByIdAsync handles invalid inputs")]
    [MemberData(nameof(UnhappyCases))]
    public Task GetByIdAsync_UnhappyPath_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return UnhappySpecs[caseId].Run(this);
    }

    private static IReadOnlyDictionary<string, UnhappySpec> BuildUnhappySpecs()
        => new Dictionary<string, UnhappySpec>
        {
            ["invalid-include-single"] = new UnhappySpec(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

                await Should.ThrowAsync<InvalidOperationException>(async () =>
                {
                    await repo.GetByIdAsync(
                        Guid.Parse(productLaptopId),
                        includeProperties: ["NotARealNavigation"],
                        includeGraph: null,
                        asNoTracking: true,
                        useSplitQuery: true,
                        cancellationToken: default);
                });
            }),
            ["invalid-include-composite"] = new UnhappySpec(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, Review>>();

                await Should.ThrowAsync<InvalidOperationException>(async () =>
                {
                    await repo.GetByIdAsync(
                        CompositeKey_ProductReview,
                        includeProperties: ["NotARealNavigation"],
                        includeGraph: null,
                        asNoTracking: true,
                        useSplitQuery: true,
                        cancellationToken: default);
                });
            }),
            ["invalid-composite-length"] = new UnhappySpec(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, Review>>();

                await Should.ThrowAsync<ArgumentException>(async () =>
                {
                    await repo.GetByIdAsync([Guid.Parse(productLaptopId)]);
                });
            }),
            ["composite-order-matters"] = new UnhappySpec(async test =>
            {
                var (response, review, _) = await test.ArrangeAndActUseingHttpForGetByIdAsync_CompositeKey<Review>(CompositeKey_ProductReview_Reversed);
                response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
                review.ShouldBeNull();
            })
        };
}
