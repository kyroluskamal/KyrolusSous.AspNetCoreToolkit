namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.PatchAsyncTests;

public partial class PatchAsyncTests
{
    [Theory(DisplayName = "PatchAsync emits observer before and after on success")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task PatchAsync_Observer_OnSuccess(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 2, comment: "patch-observer-before");
            await SeedReviewAsync(entity);

            try
            {
                var patched = await repo.PatchAsync(
                    [entity.ProductId, entity.CustomerId],
                    new Dictionary<string, object> { ["Comment"] = "patch-observer-after" });
                patched.ShouldNotBeNull();
                patched!.Comment.ShouldBe("patch-observer-after");
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var entity = CreateValidProduct(name: "patch-observer-before");
            await SeedProductAsync(entity);

            try
            {
                var patched = await repo.PatchAsync(
                    entity.Id,
                    new Dictionary<string, object> { ["Name"] = "patch-observer-after" });
                patched.ShouldNotBeNull();
                patched!.Name.ShouldBe("patch-observer-after");
            }
            finally
            {
                await CleanupProductAsync(entity.Id);
            }
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "PatchAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "PatchAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeNull();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }

    [Theory(DisplayName = "PatchAsync emits observer after with exception on cancellation")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task PatchAsync_Observer_OnFailure(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 3, comment: "patch-cancel-composite");
            await SeedReviewAsync(entity);

            try
            {
                await Should.ThrowAsync<OperationCanceledException>(async () =>
                    await repo.PatchAsync(
                        [entity.ProductId, entity.CustomerId],
                        new Dictionary<string, object> { ["Comment"] = "after" },
                        cts.Token));
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var entity = CreateValidProduct(name: "patch-cancel-single");
            await SeedProductAsync(entity);

            try
            {
                await Should.ThrowAsync<OperationCanceledException>(async () =>
                    await repo.PatchAsync(
                        entity.Id,
                        new Dictionary<string, object> { ["Name"] = "after" },
                        cts.Token));
            }
            finally
            {
                await CleanupProductAsync(entity.Id);
            }
        }

        observer.Events.Count(e => e.Stage == ObserverState.Before && e.Operation == "PatchAsync").ShouldBe(1);
        var afterEvents = observer.Events.Where(e => e.Stage == ObserverState.After && e.Operation == "PatchAsync").ToList();
        afterEvents.Count.ShouldBe(1);
        afterEvents[0].Exception.ShouldBeOfType<OperationCanceledException>();
        afterEvents[0].Duration.ShouldNotBeNull();
        observer.Reset();
    }
}
