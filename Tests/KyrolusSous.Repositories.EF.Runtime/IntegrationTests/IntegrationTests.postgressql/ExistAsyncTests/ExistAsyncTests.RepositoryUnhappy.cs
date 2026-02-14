namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.ExistAsyncTests;

public partial class ExistAsyncTests
{
    [Theory(DisplayName = "ExistAsync rejects null filters")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task ExistAsync_NullFilter_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentNullException>(async () => await repo.ExistAsync(null!));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentNullException>(async () => await singleRepo.ExistAsync(null!));
    }

    [Theory(DisplayName = "ExistAsync respects cancellation token")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task ExistAsync_CanceledToken_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await repo.ExistAsync(x => x.Rating > 0, cts.Token));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await singleRepo.ExistAsync(x => x.Price > 0m, cts.Token));
    }
}
