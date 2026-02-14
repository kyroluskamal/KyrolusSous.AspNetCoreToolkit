namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetByIdCompiledAsyncTests;

public partial class GetByIdCompiledAsyncTests
{
    public static TheoryData<string, bool, bool> ObserverCases => new()
    {
        { "success", false, false },
        { "exception", true, true }
    };

    [Theory(DisplayName = "GetByIdCompiledAsync records observer events")]
    [MemberData(nameof(ObserverCases))]
    public async Task GetByIdCompiledAsync_Observer_Events(string caseId, bool shouldThrow, bool expectExceptionAfter)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var customFactory = shouldThrow
            ? WithPolicy(new KyrolusRepositoryPolicy().SetDefaultIncludeProperties<Product>("NotARealNavigation"))
            : Factory;

        using var scope = customFactory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (shouldThrow)
        {
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await repo.GetByIdCompiledAsync(ExistingProductId));
        }
        else
        {
            (await repo.GetByIdCompiledAsync(ExistingProductId)).ShouldNotBeNull();
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "GetByIdCompiledAsync").ShouldBe(1);
        var afterEvents = observer.Events
            .Where(e => e.Stage == ObserverState.After && e.Operation == "GetByIdCompiledAsync")
            .ToList();
        afterEvents.Count.ShouldBe(1);
        if (expectExceptionAfter)
            afterEvents[0].Exception.ShouldNotBeNull();
        else
            afterEvents[0].Exception.ShouldBeNull();
    }
}
