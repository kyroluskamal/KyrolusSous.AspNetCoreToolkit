namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.AddAsyncTests;

public partial class AddAsyncTests
{
    public static TheoryData<string, bool> ObserverCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "AddAsync emits observer before and after on success")]
    [MemberData(nameof(ObserverCases))]
    public async Task AddAsync_Observer_OnSuccess(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(
                productId: DataSeeder.productBookId,
                customerId: DataSeeder.customerJohnId,
                rating: 4,
                comment: "add-observer-composite");

            var added = await repo.AddAsync(entity);
            added.ShouldNotBeNull();
            added.ProductId.ShouldBe(entity.ProductId);
            added.CustomerId.ShouldBe(entity.CustomerId);
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var entity = CreateValidProduct(name: "add-observer-single");
            var added = await repo.AddAsync(entity);
            added.ShouldNotBeNull();
            added.Id.ShouldBe(entity.Id);
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "AddAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "AddAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();

        observer.Reset();
    }

    [Theory(DisplayName = "AddAsync null entity does not emit observer notifications")]
    [MemberData(nameof(ObserverCases))]
    public async Task AddAsync_Observer_NullEntity_NoEvents(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentNullException>(async () => await repo.AddAsync(null!, default));
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            await Should.ThrowAsync<ArgumentNullException>(async () => await repo.AddAsync(null!, default));
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "AddAsync").ShouldBe(0);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "AddAsync").ToList();
        afterEvents.ShouldBeEmpty();

        observer.Reset();
    }
}
