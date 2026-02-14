namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdIncludingDeletedAsyncTests;

public partial class GetByIdIncludingDeletedAsyncTests
{
    public static TheoryData<string, bool, bool> ObserverCases => new()
    {
        { "single-success", false, false },
        { "single-exception", false, true },
        { "composite-success", true, false },
        { "composite-exception", true, true }
    };

    [Theory(DisplayName = "GetByIdIncludingDeletedAsync records observer events")]
    [MemberData(nameof(ObserverCases))]
    public async Task GetByIdIncludingDeletedAsync_Observer_Events(string caseId, bool isComposite, bool shouldThrow)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (isComposite)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            if (shouldThrow)
            {
                await Should.ThrowAsync<InvalidOperationException>(async () =>
                    await repo.GetByIdIncludingDeletedAsync(
                        ExistingReviewKey,
                        includeProperties: ["NotARealNavigation"],
                        includeGraph: null,
                        asNoTracking: true,
                        useSplitQuery: true,
                        cancellationToken: default));
            }
            else
            {
                (await repo.GetByIdIncludingDeletedAsync(ExistingReviewKey)).ShouldNotBeNull();
            }
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            if (shouldThrow)
            {
                await Should.ThrowAsync<InvalidOperationException>(async () =>
                    await repo.GetByIdIncludingDeletedAsync(
                        ExistingProductId,
                        includeProperties: ["NotARealNavigation"],
                        includeGraph: null,
                        asNoTracking: true,
                        useSplitQuery: true,
                        cancellationToken: default));
            }
            else
            {
                (await repo.GetByIdIncludingDeletedAsync(ExistingProductId)).ShouldNotBeNull();
            }
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "GetByIdIncludingDeletedAsync").ShouldBe(1);
        var afterEvents = observer.Events
            .Where(e => e.Stage == ObserverState.After && e.Operation == "GetByIdIncludingDeletedAsync")
            .ToList();
        afterEvents.Count.ShouldBe(1);
        if (shouldThrow)
            afterEvents[0].Exception.ShouldNotBeNull();
        else
            afterEvents[0].Exception.ShouldBeNull();
    }
}
