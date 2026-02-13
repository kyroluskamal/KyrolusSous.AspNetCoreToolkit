namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllCompiledAsyncTests;

public partial class GetAllCompiledAsyncTests
{
    public static TheoryData<string, bool> CancellationCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "GetAllCompiledAsync respects cancellation token")]
    [MemberData(nameof(CancellationCases))]
    public async Task GetAllCompiledAsync_CanceledToken_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await repo.GetAllCompiledAsync(r => r.Rating > 0, cancellationToken: cts.Token));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await singleRepo.GetAllCompiledAsync(p => p.Price > 0m, cancellationToken: cts.Token));
    }
}
