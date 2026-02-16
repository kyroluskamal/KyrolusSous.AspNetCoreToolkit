namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.ExecuteUpdateAsyncTests;

public partial class ExecuteUpdateAsyncTests
{
    public static TheoryData<string, bool> ObserverCases => new()
    {
        { "success", false },
        { "failure", true }
    };

    [Theory(DisplayName = "ExecuteUpdateAsync records observer events")]
    [MemberData(nameof(ObserverCases))]
    public async Task ExecuteUpdateAsync_Observer_Events(string caseId, bool shouldThrow)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var entity = CreateValidProduct(name: $"observer-{caseId}-before");
        await SeedProductAsync(entity);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
            observer.Reset();

            if (shouldThrow)
            {
                await Should.ThrowAsync<Exception>(async () =>
                    await repo.ExecuteUpdateAsync(
                        x => x.Id == entity.Id,
                        setters => setters.SetProperty(x => x.Name, x => (string)null!),
                        useSplitQuery: false));
            }
            else
            {
                var affected = await repo.ExecuteUpdateAsync(
                    x => x.Id == entity.Id,
                    setters => setters.SetProperty(x => x.Name, x => $"observer-{caseId}-after"),
                    useSplitQuery: false);
                affected.ShouldBe(1);
            }

            observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "ExecuteUpdateAsync").ShouldBe(1);
            var afterEvents = observer.Events
                .Where(e => e.Stage == ObserverState.After && e.Operation == "ExecuteUpdateAsync")
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
