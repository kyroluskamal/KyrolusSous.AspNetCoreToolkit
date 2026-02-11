namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    [Fact(DisplayName = "GetAllIncludingDeletedAsync respects cancellation token --single key")]
    public async Task GetAllIncludingDeletedAsync_CanceledToken_ThrowsOperationCanceled_SingleKey()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

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
    }
    [Fact(DisplayName = "GetAllIncludingDeletedAsync respects cancellation token --Composite key")]
    public async Task GetAllIncludingDeletedAsync_CanceledToken_ThrowsOperationCanceled_CompositeKey()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await repo.GetAllIncludingDeletedAsync(
                includeProperties: [nameof(Review.Product)],
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
                cancellationToken: cts.Token, p => p.Product);
        });
    }
}
