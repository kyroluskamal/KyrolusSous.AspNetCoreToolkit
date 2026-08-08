namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    private sealed record CancellationSpec(Func<GetByIdAsyncTests, Task> Run);

    private static readonly IReadOnlyDictionary<string, CancellationSpec> CancellationSpecs = BuildCancellationSpecs();

    public static TheoryData<string> CancellationCases => CaseIdsFrom(CancellationSpecs);

    [Theory(DisplayName = "GetByIdAsync respects cancellation token")]
    [MemberData(nameof(CancellationCases))]
    public Task GetByIdAsync_CanceledToken_ThrowsOperationCanceled(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return CancellationSpecs[caseId].Run(this);
    }

    private static IReadOnlyDictionary<string, CancellationSpec> BuildCancellationSpecs()
        => new Dictionary<string, CancellationSpec>
        {
            ["single"] = new CancellationSpec(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

                using var cts = new CancellationTokenSource();
                await cts.CancelAsync();

                await Should.ThrowAsync<OperationCanceledException>(async () =>
                {
                    await repo.GetByIdAsync(
                        Guid.Parse(productLaptopId),
                        includeProperties: ["Reviews"],
                        includeGraph: null,
                        asNoTracking: true,
                        useSplitQuery: true,
                        cancellationToken: cts.Token);
                });
            }),
            ["composite"] = new CancellationSpec(async test =>
            {
                using var scope = test.Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, Review>>();

                using var cts = new CancellationTokenSource();
                await cts.CancelAsync();

                await Should.ThrowAsync<OperationCanceledException>(async () =>
                {
                    await repo.GetByIdAsync(
                        CompositeKey_ProductReview,
                        includeProperties: ["Product"],
                        includeGraph: null,
                        asNoTracking: true,
                        useSplitQuery: true,
                        cancellationToken: cts.Token);
                });
            })
        };
}
