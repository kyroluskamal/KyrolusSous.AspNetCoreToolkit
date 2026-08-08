namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.StreamAsyncTests;

public partial class StreamAsyncTests
{
    [Theory(DisplayName = "StreamAsync respects cancellation token")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task StreamAsync_CanceledToken_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<OperationCanceledException>(async () =>
            {
                _ = await CollectAsync(repo.StreamAsync(
                    x => x.Rating > 0,
                    asNoTracking: true,
                    useSplitQuery: false,
                    cancellationToken: cts.Token));
            });
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            _ = await CollectAsync(singleRepo.StreamAsync(
                x => x.Price > 0m,
                asNoTracking: true,
                useSplitQuery: false,
                cancellationToken: cts.Token));
        });
    }

    [Fact(DisplayName = "StreamAsync records observer events on success")]
    public async Task StreamAsync_Observer_Success_Events()
    {
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var items = await CollectAsync(repo.StreamAsync(x => x.Price > 0m, asNoTracking: true, useSplitQuery: false));
        items.ShouldNotBeEmpty();

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "StreamAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "StreamAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
    }

    [Fact(DisplayName = "StreamAsync emits only before observer event when query translation fails")]
    public async Task StreamAsync_Observer_QueryFailure_OnlyBeforeEvent()
    {
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            _ = await CollectAsync(repo.StreamAsync(
                x => LocalUnsupportedPredicate(x.Name),
                asNoTracking: true,
                useSplitQuery: false));
        });

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "StreamAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "StreamAsync").ToList();
        afterEvents.ShouldBeEmpty();
    }

    private static bool LocalUnsupportedPredicate(string value)
        => value.Length > 0;
}
