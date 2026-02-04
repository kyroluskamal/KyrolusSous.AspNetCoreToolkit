namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    [Fact(DisplayName = "GetAllAsync Should record Event before and After execution")]
    public async Task GetAllAsync_ShouldRecordEventBeforeAndAfterExecution()
    {
        // Given
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();
        // When
        await repo.GetAllAsync();
        // Then
        var beforeEvent = observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "GetAllAsync");
        beforeEvent.ShouldBe(1);
        var afterEvent = observer.Events.Count(e => e.Stage == ObserverState.After && e.Operation == "GetAllAsync");
        afterEvent.ShouldBe(1);
    }

    [Fact(DisplayName = "GetAllAsync Should notify after finishing with exception if there was an error")]
    public async Task ShouldNotifyAfterFinishingWithException()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await repo.GetAllAsync(
                filter: null,
                orderBy: null,
                includeProperties: ["NotARealNavigation"],
                includeGraph: null,
                asNoTracking: true,
                useSplitQuery: true,
                cancellationToken: default);
        });
        observer.Events.Count(e => e.Stage == ObserverState.After && e.Operation == "GetAllAsync" && e.Exception is not null).ShouldBe(1);
    }
}
