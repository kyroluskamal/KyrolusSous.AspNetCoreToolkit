namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.SaveChangesAsyncTests;

public partial class SaveChangesAsyncTests
{
    [Fact(DisplayName = "SaveChangesAsync notifies observer on success")]
    public async Task SaveChangesAsync_Observer_Success()
    {
        var id = Guid.NewGuid();
        var observer = GetObserver();
        observer.Reset();

        try
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

            db.Products.Add(CreateValidProduct(id: id, name: "save-observer-success", sku: $"save-observer-{id:N}"));
            var affected = await uow.SaveChangesAsync();
            affected.ShouldBeGreaterThan(0);

            var events = observer.Events.Where(e => e.Operation == nameof(IKyrolusUnitOfWork.SaveChangesAsync)).ToList();
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

    [Fact(DisplayName = "SaveChangesAsync notifies observer on failure")]
    public async Task SaveChangesAsync_Observer_Failure()
    {
        var observer = GetObserver();
        observer.Reset();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        db.Products.Add(CreateValidProduct(
            id: Guid.NewGuid(),
            storeId: Guid.NewGuid(),
            name: "save-observer-failure",
            sku: $"save-observer-fail-{Guid.NewGuid():N}"));

        await Should.ThrowAsync<DbUpdateException>(async () => await uow.SaveChangesAsync());

        var events = observer.Events.Where(e => e.Operation == nameof(IKyrolusUnitOfWork.SaveChangesAsync)).ToList();
        events.Count.ShouldBe(2);
        events[0].Stage.ShouldBe(ObserverState.Before);
        events[1].Stage.ShouldBe(ObserverState.After);
        events[1].Exception.ShouldNotBeNull();
        events[1].Exception.ShouldBeOfType<DbUpdateException>();

        observer.Reset();
    }
}
