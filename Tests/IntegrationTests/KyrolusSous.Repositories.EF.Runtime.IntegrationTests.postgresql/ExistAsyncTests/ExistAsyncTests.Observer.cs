namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.ExistAsyncTests;

public partial class ExistAsyncTests
{
    public static TheoryData<string, bool> ObserverKeyTypeCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "ExistAsync emits observer before and after on success")]
    [MemberData(nameof(ObserverKeyTypeCases))]
    public async Task ExistAsync_Observer_OnSuccess(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var exists = await repo.ExistAsync(x => x.ProductId == DataSeeder.productLaptopId && x.CustomerId == DataSeeder.customerJaneId);
            exists.ShouldBeTrue();
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var exists = await repo.ExistAsync(x => x.Id == DataSeeder.productLaptopId);
            exists.ShouldBeTrue();
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "ExistAsync").ShouldBe(1);
        var afterEvents = observer.Events
            .Where(e => e.Stage == ObserverState.After && e.Operation == "ExistAsync")
            .ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();
        afterEvents[0].Duration!.Value.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);

        observer.Reset();
    }

    [Theory(DisplayName = "ExistAsync emits observer after with exception on failure")]
    [MemberData(nameof(ObserverKeyTypeCases))]
    public async Task ExistAsync_Observer_OnFailure(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentNullException>(async () => await repo.ExistAsync(null!));
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            await Should.ThrowAsync<ArgumentNullException>(async () => await repo.ExistAsync(null!));
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "ExistAsync").ShouldBe(1);
        var afterEvents = observer.Events
            .Where(e => e.Stage == ObserverState.After && e.Operation == "ExistAsync")
            .ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeOfType<ArgumentNullException>();
        afterEvents[0].Duration.ShouldNotBeNull();
        afterEvents[0].Duration!.Value.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);

        observer.Reset();
    }
}
