namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryPatchAsyncTests;

public partial class TryPatchAsyncTests
{
    [Theory(DisplayName = "TryPatchAsync emits observer before and after on success")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryPatchAsync_Observer_OnSuccess(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 3, comment: "try-patch-observer-before");
            await SeedReviewAsync(entity);

            try
            {
                var result = await repo.TryPatchAsync(
                    [entity.ProductId, entity.CustomerId],
                    new Dictionary<string, object> { ["Comment"] = "try-patch-observer-after" });
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                result.Exception.ShouldBeNull();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var entity = CreateValidProduct(name: "try-patch-observer-before");
            await SeedProductAsync(entity);

            try
            {
                var result = await repo.TryPatchAsync(
                    entity.Id,
                    new Dictionary<string, object> { ["Name"] = "try-patch-observer-after" });
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                result.Exception.ShouldBeNull();
            }
            finally
            {
                await CleanupProductAsync(entity.Id);
            }
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "TryPatchAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "TryPatchAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }

    [Theory(DisplayName = "TryPatchAsync emits observer before and after on not found")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryPatchAsync_Observer_OnNotFound(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var result = await repo.TryPatchAsync(
                [Guid.NewGuid(), Guid.NewGuid()],
                new Dictionary<string, object> { ["Comment"] = "x" });
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
            result.Exception.ShouldBeOfType<KeyNotFoundException>();
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var result = await repo.TryPatchAsync(
                Guid.NewGuid(),
                new Dictionary<string, object> { ["Name"] = "x" });
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
            result.Exception.ShouldBeOfType<KeyNotFoundException>();
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "TryPatchAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "TryPatchAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }
}
