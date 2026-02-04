namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    [Fact(DisplayName = "GetByIdAsync respects cancellation token")]
    public async Task GetByIdAsync_CanceledToken_ThrowsOperationCanceled()
    {
        using var scope = Factory.Services.CreateScope();
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
    }
}
