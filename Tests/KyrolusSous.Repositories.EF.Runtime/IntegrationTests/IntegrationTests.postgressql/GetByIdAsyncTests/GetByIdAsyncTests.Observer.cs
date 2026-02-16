namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdAsyncTests;

public partial class GetByIdAsyncTests
{
    public static TheoryData<string, bool> ObserverCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "GetByIdAsync emits observer before and after on success")]
    [MemberData(nameof(ObserverCases))]
    public async Task GetByIdAsync_Observer_OnSuccess(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, Review>>();
            var item = await repo.GetByIdAsync([DataSeeder.productLaptopId, DataSeeder.customerJaneId]);
            item.ShouldNotBeNull();
            item!.Rating.ShouldBe(5);
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var item = await repo.GetByIdAsync(DataSeeder.productLaptopId);
            item.ShouldNotBeNull();
            item!.Sku.ShouldBe("LP-15");
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "GetByIdAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "GetByIdAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();

        observer.Reset();
    }

    [Theory(DisplayName = "GetByIdAsync emits observer after with exception on cancellation")]
    [MemberData(nameof(ObserverCases))]
    public async Task GetByIdAsync_Observer_OnFailure(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await repo.GetByIdAsync(
                    [DataSeeder.productLaptopId, DataSeeder.customerJaneId],
                    asNoTracking: true,
                    useSplitQuery: false,
                    cancellationToken: cts.Token,
                    x => x.Product));
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await repo.GetByIdAsync(
                    DataSeeder.productLaptopId,
                    asNoTracking: true,
                    useSplitQuery: false,
                    cancellationToken: cts.Token,
                    x => x.Store));
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "GetByIdAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "GetByIdAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeOfType<OperationCanceledException>();
        afterEvents[0].Duration.ShouldNotBeNull();

        observer.Reset();
    }
}
