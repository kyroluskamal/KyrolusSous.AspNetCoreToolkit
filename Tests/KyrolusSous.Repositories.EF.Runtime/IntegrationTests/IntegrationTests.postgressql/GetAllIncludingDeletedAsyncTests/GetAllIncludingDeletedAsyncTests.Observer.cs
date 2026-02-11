namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    public static TheoryData<string, bool, bool> ObserverCases => new()
    {
        { "success", false, false },
        { "exception", true, true }
    };

    [Theory(DisplayName = "GetAllIncludingDeletedAsync records observer events")]
    [MemberData(nameof(ObserverCases))]
    public async Task GetAllIncludingDeletedAsync_Observer_Events(string caseId, bool shouldThrow, bool expectExceptionAfter)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (shouldThrow)
        {
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
        }
        else
        {
            await repo.GetAllIncludingDeletedAsync();
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "GetAllIncludingDeletedAsync").ShouldBe(1);
        var afterEvents = observer.Events
            .Where(e => e.Stage == ObserverState.After && e.Operation == "GetAllIncludingDeletedAsync")
            .ToList();
        afterEvents.Count.ShouldBe(1);
        if (expectExceptionAfter)
            afterEvents[0].Exception.ShouldNotBeNull();
        else
            afterEvents[0].Exception.ShouldBeNull();
    }
}
