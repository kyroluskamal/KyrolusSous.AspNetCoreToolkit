namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdCompiledAsyncTests;

public partial class GetByIdCompiledAsyncTests
{
    [Fact(DisplayName = "GetByIdCompiledAsync respects cancellation token")]
    public async Task GetByIdCompiledAsync_CanceledToken_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await repo.GetByIdCompiledAsync(ExistingProductId, cts.Token));
    }
}
