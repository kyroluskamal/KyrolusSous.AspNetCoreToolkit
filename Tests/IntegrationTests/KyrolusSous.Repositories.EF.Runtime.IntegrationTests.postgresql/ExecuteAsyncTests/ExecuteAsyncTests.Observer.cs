namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.ExecuteAsyncTests;

public partial class ExecuteAsyncTests
{
    [Fact(DisplayName = "ExecuteAsync triggers SaveChangesWithRetryAsync observer events on success")]
    public async Task ExecuteAsync_Observer_OnSuccess()
    {
        var id = Guid.NewGuid();
        var observer = Factory.Services.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        try
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

            var product = CreateValidProduct(id: id, name: "execute-observer-success", sku: $"execute-observer-{id:N}");
            var result = await uow.ExecuteAsync(
                work: () =>
                {
                    db.Products.Add(product);
                    return Task.CompletedTask;
                },
                useTransaction: true,
                rowVersionPropertyName: "RowVersion");

            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeGreaterThan(0);

            var events = observer.Events.Where(e => e.Operation == nameof(IKyrolusUnitOfWork.SaveChangesWithRetryAsync)).ToList();
            events.Count.ShouldBe(2);
            events[0].Stage.ShouldBe(ObserverState.Before);
            events[1].Stage.ShouldBe(ObserverState.After);
            events[1].Exception.ShouldBeNull();
        }
        finally
        {
            await CleanupProductAsync(id);
            observer.Reset();
        }
    }

    [Fact(DisplayName = "ExecuteAsync failure keeps SaveChangesWithRetryAsync observer at before stage only")]
    public async Task ExecuteAsync_Observer_OnFailure()
    {
        var observer = Factory.Services.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        var result = await uow.ExecuteAsync(
            work: () =>
            {
                db.Products.Add(CreateValidProduct(
                    id: Guid.NewGuid(),
                    storeId: Guid.NewGuid(),
                    name: "execute-observer-failure",
                    sku: $"execute-observer-failure-{Guid.NewGuid():N}"));
                return Task.CompletedTask;
            },
            useTransaction: true,
            rowVersionPropertyName: "RowVersion");

        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
        result.Exception.ShouldNotBeNull();

        var events = observer.Events.Where(e => e.Operation == nameof(IKyrolusUnitOfWork.SaveChangesWithRetryAsync)).ToList();
        events.Count.ShouldBe(1);
        events[0].Stage.ShouldBe(ObserverState.Before);

        observer.Reset();
    }
}
