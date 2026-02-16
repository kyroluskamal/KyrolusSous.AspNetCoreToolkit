namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.ExecuteDeleteAsyncTests;

public partial class ExecuteDeleteAsyncTests
{
    public static TheoryData<string, bool> ObserverCases => new()
    {
        { "success", false },
        { "failure", true }
    };

    [Theory(DisplayName = "ExecuteDeleteAsync records observer events")]
    [MemberData(nameof(ObserverCases))]
    public async Task ExecuteDeleteAsync_Observer_Events(string caseId, bool shouldThrow)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var entity = CreateValidProduct(name: $"observer-{caseId}-delete", sku: $"EXD-OBS-{Guid.NewGuid():N}");
        await SeedProductAsync(entity);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
            observer.Reset();

            if (shouldThrow)
            {
                using var cts = new CancellationTokenSource();
                await cts.CancelAsync();
                await Should.ThrowAsync<OperationCanceledException>(async () =>
                    await repo.ExecuteDeleteAsync(x => x.Id == entity.Id, cancellationToken: cts.Token));
            }
            else
            {
                var affected = await repo.ExecuteDeleteAsync(x => x.Id == entity.Id);
                affected.ShouldBe(1);
            }

            observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "ExecuteDeleteAsync").ShouldBe(1);
            var afterEvents = observer.Events
                .Where(e => e.Stage == ObserverState.After && e.Operation == "ExecuteDeleteAsync")
                .ToList();
            afterEvents.Count.ShouldBe(1);

            if (shouldThrow)
                afterEvents[0].Exception.ShouldNotBeNull();
            else
                afterEvents[0].Exception.ShouldBeNull();
        }
        finally
        {
            await CleanupProductAsync(entity.Id);
        }
    }
}
