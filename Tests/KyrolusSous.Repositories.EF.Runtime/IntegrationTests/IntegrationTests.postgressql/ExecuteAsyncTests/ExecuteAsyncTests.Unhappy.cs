namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.ExecuteAsyncTests;

public partial class ExecuteAsyncTests
{
    [Theory(DisplayName = "ExecuteAsync rejects null work delegate")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_NullWork_Throws(bool useTransaction)
    {
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await uow.ExecuteAsync(null!, useTransaction: useTransaction));
    }

    [Theory(DisplayName = "ExecuteAsync propagates exceptions thrown by work")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_WorkThrows_Propagates(bool useTransaction)
    {
        var id = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await uow.ExecuteAsync(
                work: () =>
                {
                    db.Products.Add(CreateValidProduct(id: id, name: "execute-work-throws"));
                    throw new InvalidOperationException("work-failed");
                },
                useTransaction: useTransaction));

        (await FindProductAsync(id)).ShouldBeNull();
    }

    [Theory(DisplayName = "ExecuteAsync returns failed for save errors")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_SaveFailure_ReturnsFailed(bool useTransaction)
    {
        var id = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        var result = await uow.ExecuteAsync(
            work: () =>
            {
                db.Products.Add(CreateValidProduct(
                    id: id,
                    storeId: Guid.NewGuid(),
                    name: "execute-save-failure",
                    sku: $"execute-fail-{id:N}"));
                return Task.CompletedTask;
            },
            useTransaction: useTransaction);

        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
        result.Exception.ShouldBeOfType<DbUpdateException>();
        result.Value.ShouldBe(0);
        (await FindProductAsync(id)).ShouldBeNull();
    }

    [Theory(DisplayName = "ExecuteAsync with transaction throws when token is canceled before start")]
    [InlineData(true)]
    public async Task ExecuteAsync_CanceledToken_WithTransaction_Throws(bool useTransaction)
    {
        useTransaction.ShouldBeTrue();
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await uow.ExecuteAsync(
                work: () => Task.CompletedTask,
                useTransaction: useTransaction,
                cancellationToken: cts.Token));
    }

    [Theory(DisplayName = "ExecuteAsync without transaction returns failed for canceled token")]
    [InlineData(false)]
    public async Task ExecuteAsync_CanceledToken_WithoutTransaction_ReturnsFailed(bool useTransaction)
    {
        useTransaction.ShouldBeFalse();
        var id = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await uow.ExecuteAsync(
            work: () =>
            {
                db.Products.Add(CreateValidProduct(id: id, name: "execute-cancel-no-tx"));
                return Task.CompletedTask;
            },
            useTransaction: useTransaction,
            cancellationToken: cts.Token);

        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
        result.Exception.ShouldBeOfType<OperationCanceledException>();
        result.Value.ShouldBe(0);
        (await FindProductAsync(id)).ShouldBeNull();
    }

    [Fact(DisplayName = "ExecuteAsync returns concurrency conflict when token is stale")]
    public async Task ExecuteAsync_ConcurrencyConflict_ReturnsConflict()
    {
        var id = Guid.NewGuid();
        await SeedProductAsync(CreateValidProduct(
            id: id,
            name: "execute-concurrency-seed",
            sku: $"execute-concurrency-{id:N}"));

        try
        {
            using var scopeWinner = Factory.Services.CreateScope();
            using var scopeStale = Factory.Services.CreateScope();
            var dbWinner = scopeWinner.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbStale = scopeStale.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var uowStale = scopeStale.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

            var winner = await dbWinner.Products.SingleAsync(x => x.Id == id);
            var stale = await dbStale.Products.SingleAsync(x => x.Id == id);

            winner.Name = "execute-concurrency-winner";
            winner.RowVersion = [9];
            await dbWinner.SaveChangesAsync();

            var result = await uowStale.ExecuteAsync(
                work: () =>
                {
                    stale.Name = "execute-concurrency-stale";
                    return Task.CompletedTask;
                },
                useTransaction: false,
                rowVersionPropertyName: "RowVersion");

            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.ConcurrencyConflict);
            result.Exception.ShouldBeOfType<DbUpdateConcurrencyException>();
            result.Value.ShouldBe(0);
            result.Concurrency.ShouldNotBeNull();
            result.Concurrency.Value.RetryCount.ShouldBe(0);
            result.Concurrency.Value.OriginalRowVersion.ShouldNotBeNull();
            result.Concurrency.Value.CurrentRowVersion.ShouldNotBeNull();
        }
        finally
        {
            await CleanupProductAsync(id);
        }
    }
}
