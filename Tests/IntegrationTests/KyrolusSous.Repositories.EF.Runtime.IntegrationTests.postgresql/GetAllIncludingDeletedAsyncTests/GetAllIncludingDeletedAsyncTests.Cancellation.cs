namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    public static TheoryData<string> CancellationCases =>
    [
        "product",
        "review"
    ];

    [Theory(DisplayName = "GetAllIncludingDeletedAsync respects cancellation token")]
    [MemberData(nameof(CancellationCases))]
    public async Task GetAllIncludingDeletedAsync_CanceledToken_ThrowsOperationCanceled(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (caseId == "product")
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

            await Should.ThrowAsync<OperationCanceledException>(async () =>
            {
                await repo.GetAllIncludingDeletedAsync(
                    includeProperties: [nameof(Product.Reviews)],
                    asNoTracking: true,
                    useSplitQuery: true,
                    cancellationToken: cts.Token);
            });
            await Should.ThrowAsync<OperationCanceledException>(async () =>
            {
                await repo.GetAllIncludingDeletedAsync(
                    null, null,
                    asNoTracking: true,
                    useSplitQuery: true,
                    cancellationToken: cts.Token, p => p.Reviews);
            });
            return;
        }

        var compositeRepo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await compositeRepo.GetAllIncludingDeletedAsync(
                includeProperties: [nameof(Review.Product)],
                asNoTracking: true,
                useSplitQuery: true,
                cancellationToken: cts.Token);
        });
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await compositeRepo.GetAllIncludingDeletedAsync(
                null, null,
                asNoTracking: true,
                useSplitQuery: true,
                cancellationToken: cts.Token, p => p.Product);
        });
    }
}
