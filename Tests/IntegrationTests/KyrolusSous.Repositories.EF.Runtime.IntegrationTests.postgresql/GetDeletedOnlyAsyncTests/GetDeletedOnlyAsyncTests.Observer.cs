namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetDeletedOnlyAsyncTests;

public partial class GetDeletedOnlyAsyncTests
{
    [Theory(DisplayName = "GetDeletedOnlyAsync emits observer before and after on success")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetDeletedOnlyAsync_Observer_OnSuccess(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
            var entity = CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 2, comment: "gdo-observer-composite");
            await SeedReviewAsync(entity);

            try
            {
                (await repo.SoftDeleteAsync([entity.ProductId, entity.CustomerId])).ShouldBeTrue();
                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
                observer.Reset();

                var items = await repo.GetDeletedOnlyAsync(x => x.ProductId == entity.ProductId && x.CustomerId == entity.CustomerId);
                items.Count.ShouldBe(1);
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
            var entity = CreateValidProduct(name: "gdo-observer-single");
            await SeedProductAsync(entity);

            try
            {
                (await repo.SoftDeleteAsync(entity.Id)).ShouldBeTrue();
                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
                observer.Reset();

                var items = await repo.GetDeletedOnlyAsync(x => x.Id == entity.Id);
                items.Count.ShouldBe(1);
            }
            finally
            {
                await CleanupProductAsync(entity.Id);
            }
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "GetDeletedOnlyAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "GetDeletedOnlyAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }

    [Theory(DisplayName = "GetDeletedOnlyAsync emits observer after with exception on cancellation")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetDeletedOnlyAsync_Observer_OnFailure(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var compositeKey = KeyTypeSpecs[caseId];

        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await repo.GetDeletedOnlyAsync(
                    x => x.Rating > 0,
                    cancellationToken: cts.Token));
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await repo.GetDeletedOnlyAsync(
                    x => x.Price > 0m,
                    cancellationToken: cts.Token));
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "GetDeletedOnlyAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "GetDeletedOnlyAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeOfType<OperationCanceledException>();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }
}
