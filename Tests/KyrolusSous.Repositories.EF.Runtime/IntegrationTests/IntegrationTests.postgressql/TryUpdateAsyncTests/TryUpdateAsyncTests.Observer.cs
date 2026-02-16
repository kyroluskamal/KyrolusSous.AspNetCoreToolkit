namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryUpdateAsyncTests;

public partial class TryUpdateAsyncTests
{
    [Theory(DisplayName = "TryUpdateAsync emits observer before and after on success")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryUpdateAsync_Observer_OnSuccess(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "try-update-observer-before");
            await SeedReviewAsync(entity);

            try
            {
                var updated = Clone(entity);
                updated.Rating = 5;
                updated.Comment = "try-update-observer-after";

                var result = await repo.TryUpdateAsync(updated);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                result.Exception.ShouldBeNull();
                result.Value.ShouldNotBeNull();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var entity = CreateValidProduct(name: "try-update-observer-before");
            await SeedProductAsync(entity);

            try
            {
                var updated = Clone(entity);
                updated.Name = "try-update-observer-after";

                var result = await repo.TryUpdateAsync(updated);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                result.Exception.ShouldBeNull();
                result.Value.ShouldNotBeNull();
            }
            finally
            {
                await CleanupProductAsync(entity.Id);
            }
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "TryUpdateAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "TryUpdateAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }

    [Theory(DisplayName = "TryUpdateAsync emits observer before and after on not found")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryUpdateAsync_Observer_OnNotFound(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var missing = CreateValidReview(Guid.NewGuid(), Guid.NewGuid(), rating: 1, comment: "missing");
            var result = await repo.TryUpdateAsync(missing);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
            result.Exception.ShouldBeOfType<KeyNotFoundException>();
            result.Value.ShouldBeNull();
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var missing = CreateValidProduct(id: Guid.NewGuid(), name: "missing");
            var result = await repo.TryUpdateAsync(missing);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
            result.Exception.ShouldBeOfType<KeyNotFoundException>();
            result.Value.ShouldBeNull();
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "TryUpdateAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "TryUpdateAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }
}
