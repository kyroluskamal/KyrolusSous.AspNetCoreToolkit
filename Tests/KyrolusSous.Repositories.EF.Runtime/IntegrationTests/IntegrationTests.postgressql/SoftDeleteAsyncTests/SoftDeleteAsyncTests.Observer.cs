namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.SoftDeleteAsyncTests;

public partial class SoftDeleteAsyncTests
{
    [Theory(DisplayName = "SoftDeleteAsync emits observer before and after on success")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task SoftDeleteAsync_Observer_OnSuccess(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "softdelete-observer-composite");
            await SeedReviewAsync(entity);

            try
            {
                var deleted = await repo.SoftDeleteAsync([entity.ProductId, entity.CustomerId]);
                deleted.ShouldBeTrue();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var entity = CreateValidProduct(name: "softdelete-observer-single");
            await SeedProductAsync(entity);

            try
            {
                var deleted = await repo.SoftDeleteAsync(entity.Id);
                deleted.ShouldBeTrue();
            }
            finally
            {
                await CleanupProductAsync(entity.Id);
            }
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "SoftDeleteAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "SoftDeleteAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }

    [Theory(DisplayName = "SoftDeleteAsync emits observer after with exception on cancellation")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task SoftDeleteAsync_Observer_OnFailure(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 3, comment: "softdelete-cancel-composite");
            await SeedReviewAsync(entity);

            try
            {
                await Should.ThrowAsync<OperationCanceledException>(async () =>
                    await repo.SoftDeleteAsync([entity.ProductId, entity.CustomerId], cts.Token));
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var entity = CreateValidProduct(name: "softdelete-cancel-single");
            await SeedProductAsync(entity);

            try
            {
                await Should.ThrowAsync<OperationCanceledException>(async () =>
                    await repo.SoftDeleteAsync(entity.Id, cts.Token));
            }
            finally
            {
                await CleanupProductAsync(entity.Id);
            }
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "SoftDeleteAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "SoftDeleteAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeOfType<OperationCanceledException>();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }
}
