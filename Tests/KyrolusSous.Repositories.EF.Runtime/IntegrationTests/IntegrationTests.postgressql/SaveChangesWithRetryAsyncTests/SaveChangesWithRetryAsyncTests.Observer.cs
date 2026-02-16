namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.SaveChangesWithRetryAsyncTests;

public partial class SaveChangesWithRetryAsyncTests
{
    [Fact(DisplayName = "SaveChangesWithRetryAsync notifies observer on success")]
    public async Task SaveChangesWithRetryAsync_Observer_Success()
    {
        var id = Guid.NewGuid();
        var observer = GetObserver();
        observer.Reset();

        try
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

            db.Products.Add(CreateValidProduct(id: id, name: "uow-observer-success", sku: $"uow-observer-{id:N}"));
            var result = await uow.SaveChangesWithRetryAsync(rowVersionPropertyName: "RowVersion");

            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);

            var events = observer.Events.Where(e => e.Operation == nameof(IKyrolusUnitOfWork.SaveChangesWithRetryAsync)).ToList();
            events.Count.ShouldBe(2);
            events[0].Stage.ShouldBe(ObserverState.Before);
            events[1].Stage.ShouldBe(ObserverState.After);
            events[1].Exception.ShouldBeNull();
            events[1].Payload.ShouldBeOfType<int>();
        }
        finally
        {
            await CleanupProductAsync(id);
            observer.Reset();
        }
    }

    [Fact(DisplayName = "SaveChangesWithRetryAsync failure records observer before event")]
    public async Task SaveChangesWithRetryAsync_Observer_Failure_BeforeOnly()
    {
        var observer = GetObserver();
        observer.Reset();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        db.Products.Add(CreateValidProduct(
            id: Guid.NewGuid(),
            storeId: Guid.NewGuid(),
            name: "uow-observer-failure",
            sku: $"uow-observer-fail-{Guid.NewGuid():N}"));

        var result = await uow.SaveChangesWithRetryAsync(rowVersionPropertyName: "RowVersion");
        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);

        var events = observer.Events.Where(e => e.Operation == nameof(IKyrolusUnitOfWork.SaveChangesWithRetryAsync)).ToList();
        events.Count.ShouldBe(1);
        events[0].Stage.ShouldBe(ObserverState.Before);

        observer.Reset();
    }

    [Fact(DisplayName = "SaveChangesWithRetryAsync uses configured retry count in concurrency conflicts")]
    public async Task SaveChangesWithRetryAsync_ConcurrencyConflict_UsesConfiguredRetryCount()
    {
        var id = Guid.NewGuid();
        var customFactory = WithPolicy(new KyrolusRepositoryPolicy
        {
            ConcurrencyRetryCount = 2,
            RowVersionProperty = "RowVersion"
        });

        try
        {
            using (var seedScope = customFactory.Services.CreateScope())
            {
                var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                seedDb.Products.Add(CreateValidProduct(
                    id: id,
                    name: "uow-retry-policy-seed",
                    sku: $"uow-retry-policy-{id:N}"));
                await seedDb.SaveChangesAsync();
            }

            using var scope1 = customFactory.Services.CreateScope();
            using var scope2 = customFactory.Services.CreateScope();

            var db1 = scope1.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var uow2 = scope2.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

            var winner = await db1.Products.SingleAsync(x => x.Id == id);
            var stale = await db2.Products.SingleAsync(x => x.Id == id);

            winner.Name = "uow-retry-policy-winner";
            winner.RowVersion = [7];
            await db1.SaveChangesAsync();

            stale.Name = "uow-retry-policy-stale";
            var result = await uow2.SaveChangesWithRetryAsync(rowVersionPropertyName: "RowVersion");

            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.ConcurrencyConflict);
            result.Exception.ShouldBeOfType<DbUpdateConcurrencyException>();
            result.Concurrency.ShouldNotBeNull();
            result.Concurrency.Value.RetryCount.ShouldBe(2);
        }
        finally
        {
            using var cleanupScope = customFactory.Services.CreateScope();
            var cleanupDb = cleanupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var entity = await cleanupDb.Products.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id);
            if (entity is not null)
            {
                cleanupDb.Products.Remove(entity);
                await cleanupDb.SaveChangesAsync();
            }
        }
    }
}
