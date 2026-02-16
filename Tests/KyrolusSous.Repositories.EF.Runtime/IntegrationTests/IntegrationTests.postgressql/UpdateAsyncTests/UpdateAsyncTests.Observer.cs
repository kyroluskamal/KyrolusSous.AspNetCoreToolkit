namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.UpdateAsyncTests;

public partial class UpdateAsyncTests
{
    public static TheoryData<string, bool> ObserverCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "UpdateAsync emits observer before and after on success")]
    [MemberData(nameof(ObserverCases))]
    public async Task UpdateAsync_Observer_OnSuccess(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var seed = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "update-observer-before");
            await SeedReviewAsync(seed);

            try
            {
                var updated = CreateValidReview(seed.ProductId, seed.CustomerId, rating: 5, comment: "update-observer-after");
                var result = await repo.UpdateAsync(updated);
                result.ShouldNotBeNull();
                result.Rating.ShouldBe(5);
            }
            finally
            {
                await CleanupReviewAsync(seed.ProductId, seed.CustomerId);
            }
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var seed = CreateValidProduct(name: "update-observer-before");
            await SeedProductAsync(seed);

            try
            {
                var updated = CreateValidProduct(
                    id: seed.Id,
                    storeId: seed.StoreId,
                    sku: seed.Sku,
                    name: "update-observer-after");
                var result = await repo.UpdateAsync(updated);
                result.ShouldNotBeNull();
                result.Name.ShouldBe("update-observer-after");
            }
            finally
            {
                await CleanupProductAsync(seed.Id);
            }
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "UpdateAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "UpdateAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }

    [Theory(DisplayName = "UpdateAsync emits observer after with exception when entity is missing")]
    [MemberData(nameof(ObserverCases))]
    public async Task UpdateAsync_Observer_OnFailure(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var missing = CreateValidReview(Guid.NewGuid(), Guid.NewGuid(), rating: 1, comment: "missing");
            await Should.ThrowAsync<KeyNotFoundException>(async () => await repo.UpdateAsync(missing));
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var missing = CreateValidProduct(id: Guid.NewGuid(), name: "missing");
            await Should.ThrowAsync<KeyNotFoundException>(async () => await repo.UpdateAsync(missing));
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "UpdateAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "UpdateAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeOfType<KeyNotFoundException>();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }
}
