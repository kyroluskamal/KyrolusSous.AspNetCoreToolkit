namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.AddRangeAsyncTests;

public partial class AddRangeAsyncTests
{
    public static TheoryData<string, bool> ObserverCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "AddRangeAsync emits observer before and after on success")]
    [MemberData(nameof(ObserverCases))]
    public async Task AddRangeAsync_Observer_OnSuccess(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entities = new List<Review>
            {
                CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "add-range-observer-composite-1"),
                CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 4, comment: "add-range-observer-composite-2")
            };

            var added = (await repo.AddRangeAsync(entities)).ToList();
            added.Count.ShouldBe(2);
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var entities = new List<Product>
            {
                CreateValidProduct(name: "add-range-observer-single-1"),
                CreateValidProduct(name: "add-range-observer-single-2")
            };

            var added = (await repo.AddRangeAsync(entities)).ToList();
            added.Count.ShouldBe(2);
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "AddRangeAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "AddRangeAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();

        observer.Reset();
    }

    [Theory(DisplayName = "AddRangeAsync invalid collections do not emit observer notifications")]
    [MemberData(nameof(ObserverCases))]
    public async Task AddRangeAsync_Observer_InvalidInput_NoEvents(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await repo.AddRangeAsync(Array.Empty<Review>(), default));
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await repo.AddRangeAsync(Array.Empty<Product>(), default));
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "AddRangeAsync").ShouldBe(0);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "AddRangeAsync").ToList();
        afterEvents.ShouldBeEmpty();

        observer.Reset();
    }
}
