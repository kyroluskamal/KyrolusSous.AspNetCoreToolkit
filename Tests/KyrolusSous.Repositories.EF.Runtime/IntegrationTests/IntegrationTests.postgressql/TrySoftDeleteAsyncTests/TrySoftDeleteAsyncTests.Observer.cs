namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TrySoftDeleteAsyncTests;

public partial class TrySoftDeleteAsyncTests
{
    [Theory(DisplayName = "TrySoftDeleteAsync emits observer before and after on success")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TrySoftDeleteAsync_Observer_OnSuccess(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 3, comment: "try-softdelete-observer-composite");
            await SeedReviewAsync(entity);

            try
            {
                var result = await repo.TrySoftDeleteAsync([entity.ProductId, entity.CustomerId]);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                result.Exception.ShouldBeNull();
                result.Value.ShouldBeTrue();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var entity = CreateValidProduct(name: "try-softdelete-observer-single");
            await SeedProductAsync(entity);

            try
            {
                var result = await repo.TrySoftDeleteAsync(entity.Id);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                result.Exception.ShouldBeNull();
                result.Value.ShouldBeTrue();
            }
            finally
            {
                await CleanupProductAsync(entity.Id);
            }
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "TrySoftDeleteAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "TrySoftDeleteAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }

    [Theory(DisplayName = "TrySoftDeleteAsync emits observer before and after on not found")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TrySoftDeleteAsync_Observer_OnNotFound(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var result = await repo.TrySoftDeleteAsync([Guid.NewGuid(), Guid.NewGuid()]);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.NotFound);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeFalse();
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var result = await repo.TrySoftDeleteAsync(Guid.NewGuid());
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.NotFound);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeFalse();
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "TrySoftDeleteAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "TrySoftDeleteAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }
}
