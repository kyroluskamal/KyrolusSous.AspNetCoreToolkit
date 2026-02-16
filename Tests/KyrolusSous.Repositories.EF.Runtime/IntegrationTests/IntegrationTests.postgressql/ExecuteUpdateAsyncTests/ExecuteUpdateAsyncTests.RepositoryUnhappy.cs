namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.ExecuteUpdateAsyncTests;

public partial class ExecuteUpdateAsyncTests
{
    public static TheoryData<string, bool> KeyTypeCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "ExecuteUpdateAsync rejects null setPropertyCalls")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task ExecuteUpdateAsync_NullSetters_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentNullException>(async () =>
                await repo.ExecuteUpdateAsync(x => x.Rating > 0, setPropertyCalls: null!));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await singleRepo.ExecuteUpdateAsync(x => x.Price > 0m, setPropertyCalls: null!));
    }

    [Theory(DisplayName = "ExecuteUpdateAsync respects cancellation token")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task ExecuteUpdateAsync_CanceledToken_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await repo.ExecuteUpdateAsync(
                    x => x.Rating > 0,
                    setters => setters.SetProperty(x => x.Rating, x => x.Rating),
                    useSplitQuery: false,
                    cancellationToken: cts.Token));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await singleRepo.ExecuteUpdateAsync(
                x => x.Price > 0m,
                setters => setters.SetProperty(x => x.Price, x => x.Price),
                useSplitQuery: false,
                cancellationToken: cts.Token));
    }

    [Theory(DisplayName = "ExecuteUpdateAsync propagates setter callback exceptions")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task ExecuteUpdateAsync_SetterCallbackThrows_Propagates(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await repo.ExecuteUpdateAsync(
                    x => x.Rating > 0,
                    _ => throw new InvalidOperationException("boom")));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await singleRepo.ExecuteUpdateAsync(
                x => x.Price > 0m,
                _ => throw new InvalidOperationException("boom")));
    }

    [Fact(DisplayName = "ExecuteUpdateAsync throws when setting required single-key property to null")]
    public async Task ExecuteUpdateAsync_SetRequiredPropertyToNull_Throws()
    {
        var entity = CreateValidProduct(name: "required-before");
        await SeedProductAsync(entity);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

            await Should.ThrowAsync<Exception>(async () =>
                await repo.ExecuteUpdateAsync(
                    x => x.Id == entity.Id,
                    setters => setters.SetProperty(x => x.Name, x => (string)null!),
                    useSplitQuery: false));
        }
        finally
        {
            await CleanupProductAsync(entity.Id);
        }
    }
}
