namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    public sealed record ObserverCase(string CaseId, bool ShouldThrow, bool ExpectExceptionAfter);

    public static TheoryData<ObserverCase> ObserverCases =>
    [
        new ObserverCase("success", false, false),
        new ObserverCase("exception", true, true)
    ];

    [Theory(DisplayName = "GetAllIncludingDeletedAsync records observer events")]
    [MemberData(nameof(ObserverCases))]
    public async Task GetAllIncludingDeletedAsync_Observer_Events(ObserverCase testCase)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (testCase.ShouldThrow)
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
        if (testCase.ExpectExceptionAfter)
            afterEvents[0].Exception.ShouldNotBeNull();
        else
            afterEvents[0].Exception.ShouldBeNull();
    }
}
