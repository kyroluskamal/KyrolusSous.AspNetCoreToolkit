namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    public sealed record CancellationCase(KeyType KeyType, string IncludeProperty);

    public static TheoryData<CancellationCase> CancellationCases => new()
    {
        new CancellationCase(KeyType.Single, nameof(Product.Reviews)),
        new CancellationCase(KeyType.Composite, nameof(Review.Product))
    };

    [Theory(DisplayName = "GetAllIncludingDeletedAsync respects cancellation token")]
    [MemberData(nameof(CancellationCases))]
    public async Task GetAllIncludingDeletedAsync_CanceledToken_ThrowsOperationCanceled(CancellationCase testCase)
    {
        using var scope = Factory.Services.CreateScope();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (testCase.KeyType == KeyType.Single)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

            await Should.ThrowAsync<OperationCanceledException>(async () =>
            {
                await repo.GetAllIncludingDeletedAsync(
                    includeProperties: [testCase.IncludeProperty],
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
                includeProperties: [testCase.IncludeProperty],
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
