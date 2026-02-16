namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.SaveChangesWithRetryAsyncTests;

public partial class SaveChangesWithRetryAsyncTests
{
    [Fact(DisplayName = "SaveChangesWithRetryAsync returns failed for DbUpdateException")]
    public async Task SaveChangesWithRetryAsync_DbUpdateFailure_ReturnsFailed()
    {
        var id = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        db.Products.Add(CreateValidProduct(
            id: id,
            storeId: Guid.NewGuid(),
            name: "uow-failure-invalid-store",
            sku: $"uow-failure-{id:N}"));

        var result = await uow.SaveChangesWithRetryAsync(rowVersionPropertyName: "RowVersion");

        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
        result.Exception.ShouldBeOfType<DbUpdateException>();
        result.Value.ShouldBe(0);
        result.Concurrency.ShouldBeNull();
        (await FindProductAsync(id)).ShouldBeNull();
    }

    [Fact(DisplayName = "SaveChangesWithRetryAsync returns failed for canceled token")]
    public async Task SaveChangesWithRetryAsync_CanceledToken_ReturnsFailed()
    {
        var id = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        db.Products.Add(CreateValidProduct(
            id: id,
            name: "uow-canceled",
            sku: $"uow-canceled-{id:N}"));

        var result = await uow.SaveChangesWithRetryAsync(cancellationToken: cts.Token);

        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
        result.Exception.ShouldBeOfType<OperationCanceledException>();
        result.Value.ShouldBe(0);
        result.Concurrency.ShouldBeNull();
        (await FindProductAsync(id)).ShouldBeNull();
    }

    [Fact(DisplayName = "SaveChangesWithRetryAsync returns concurrency conflict when token is stale")]
    public async Task SaveChangesWithRetryAsync_ConcurrencyConflict_ReturnsConflict()
    {
        var id = Guid.NewGuid();
        await SeedProductAsync(CreateValidProduct(
            id: id,
            name: "uow-concurrency-seed",
            sku: $"uow-concurrency-{id:N}"));

        try
        {
            using var scope1 = Factory.Services.CreateScope();
            using var scope2 = Factory.Services.CreateScope();

            var db1 = scope1.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var uow2 = scope2.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

            var winner = await db1.Products.SingleAsync(x => x.Id == id);
            var stale = await db2.Products.SingleAsync(x => x.Id == id);

            winner.Name = "uow-concurrency-winner";
            winner.RowVersion = [1];
            await db1.SaveChangesAsync();

            stale.Name = "uow-concurrency-stale";

            var result = await uow2.SaveChangesWithRetryAsync(rowVersionPropertyName: "RowVersion");

            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.ConcurrencyConflict);
            result.Exception.ShouldBeOfType<DbUpdateConcurrencyException>();
            result.Value.ShouldBe(0);
            result.Concurrency.ShouldNotBeNull();
            result.Concurrency.Value.RetryCount.ShouldBe(0);
            result.Concurrency.Value.OriginalRowVersion.ShouldNotBeNull();
            result.Concurrency.Value.CurrentRowVersion.ShouldNotBeNull();

            var persisted = await FindProductAsync(id);
            persisted.ShouldNotBeNull();
            persisted!.Name.ShouldBe("uow-concurrency-winner");
            persisted.RowVersion.ShouldBe([1]);
        }
        finally
        {
            await CleanupProductAsync(id);
        }
    }
}
