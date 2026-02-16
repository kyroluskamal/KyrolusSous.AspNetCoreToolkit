namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.RestoreAsyncTests;

public partial class RestoreAsyncTests
{
    [Theory(DisplayName = "RestoreAsync emits observer before and after on success")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task RestoreAsync_Observer_OnSuccess(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 4, comment: "restore-observer-composite");
            await SeedReviewAsync(entity);

            try
            {
                (await repo.SoftDeleteAsync([entity.ProductId, entity.CustomerId])).ShouldBeTrue();
                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
                observer.Reset();

                var restored = await repo.RestoreAsync([entity.ProductId, entity.CustomerId]);
                restored.ShouldBeTrue();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var entity = CreateValidProduct(name: "restore-observer-single");
            await SeedProductAsync(entity);

            try
            {
                (await repo.SoftDeleteAsync(entity.Id)).ShouldBeTrue();
                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
                observer.Reset();

                var restored = await repo.RestoreAsync(entity.Id);
                restored.ShouldBeTrue();
            }
            finally
            {
                await CleanupProductAsync(entity.Id);
            }
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "RestoreAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "RestoreAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }

    [Theory(DisplayName = "RestoreAsync emits observer after with exception on cancellation")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task RestoreAsync_Observer_OnFailure(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var compositeKey = KeyTypeSpecs[caseId];

        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var product = CreateValidProduct(name: "restore-cancel-product");
            await SeedProductAsync(product);
            var entity = CreateValidReview(product.Id, DataSeeder.customerJohnId, rating: 3, comment: "restore-cancel-composite");
            await SeedReviewAsync(entity);

            try
            {
                (await repo.SoftDeleteAsync([entity.ProductId, entity.CustomerId])).ShouldBeTrue();
                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
                observer.Reset();

                await Should.ThrowAsync<OperationCanceledException>(async () =>
                    await repo.RestoreAsync([entity.ProductId, entity.CustomerId], cts.Token));
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
                await CleanupProductAsync(product.Id);
            }
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var entity = CreateValidProduct(name: "restore-cancel-single");
            await SeedProductAsync(entity);

            try
            {
                (await repo.SoftDeleteAsync(entity.Id)).ShouldBeTrue();
                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
                observer.Reset();

                await Should.ThrowAsync<OperationCanceledException>(async () =>
                    await repo.RestoreAsync(entity.Id, cts.Token));
            }
            finally
            {
                await CleanupProductAsync(entity.Id);
            }
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "RestoreAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "RestoreAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeOfType<OperationCanceledException>();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }
}
