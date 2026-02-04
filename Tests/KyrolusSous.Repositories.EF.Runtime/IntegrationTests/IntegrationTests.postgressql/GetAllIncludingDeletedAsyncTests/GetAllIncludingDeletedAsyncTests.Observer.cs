namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    [Fact(DisplayName = "GetAllIncludingDeletedAsync records before/after observer events")]
    public async Task GetAllIncludingDeletedAsync_Observer_Events()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        await repo.GetAllIncludingDeletedAsync();

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "GetAllIncludingDeletedAsync").ShouldBe(1);
        observer.Events.Count(e => e.Stage == ObserverState.After && e.Operation == "GetAllIncludingDeletedAsync").ShouldBe(1);
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync notifies observer after exception")]
    public async Task GetAllIncludingDeletedAsync_Observer_Exception_Notified()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await repo.GetAllIncludingDeletedAsync(
                filter: null,
                orderBy: null,
                includeProperties: ["NotARealNavigation"],
                includeGraph: null,
                asNoTracking: true,
                useSplitQuery: true,
                cancellationToken: default);
        });

        observer.Events.Count(e => e.Stage == ObserverState.After && e.Operation == "GetAllIncludingDeletedAsync" && e.Exception is not null).ShouldBe(1);
    }
}
