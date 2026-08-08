namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryRemoveAsyncTests;

public partial class TryRemoveAsyncTests
{
    private static readonly IReadOnlyDictionary<string, bool> ObserverKeyTypeSpecs = BuildObserverKeyTypeSpecs();
    public static TheoryData<string> ObserverKeyTypeCases => CaseIdsFrom(ObserverKeyTypeSpecs);

    [Theory(DisplayName = "TryRemoveAsync by key emits observer before and after on success")]
    [MemberData(nameof(ObserverKeyTypeCases))]
    public async Task TryRemoveAsync_ByKey_Observer_OnSuccess(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = ObserverKeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
            var entity = CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 2, comment: "try-remove-observer-composite");
            await SeedReviewAsync(entity);

            try
            {
                var result = await repo.TryRemoveAsync([entity.ProductId, entity.CustomerId]);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                result.Exception.ShouldBeNull();
                result.Value.ShouldBeTrue();
                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);

                var events = observer.Events.Where(e => e.Operation == nameof(repo.TryRemoveAsync)).ToList();
                events.Count.ShouldBe(2);
                events[0].Stage.ShouldBe(ObserverState.Before);
                events[1].Stage.ShouldBe(ObserverState.After);
                events[1].Exception.ShouldBeNull();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
                observer.Reset();
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleUow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        var product = CreateValidProduct(name: "try-remove-observer-single");
        await SeedProductAsync(product);

        try
        {
            var result = await singleRepo.TryRemoveAsync(product.Id);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeTrue();
            (await singleUow.SaveChangesAsync()).ShouldBeGreaterThan(0);

            var events = observer.Events.Where(e => e.Operation == nameof(singleRepo.TryRemoveAsync)).ToList();
            events.Count.ShouldBe(2);
            events[0].Stage.ShouldBe(ObserverState.Before);
            events[1].Stage.ShouldBe(ObserverState.After);
            events[1].Exception.ShouldBeNull();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
            observer.Reset();
        }
    }

    [Theory(DisplayName = "TryRemoveAsync by key emits observer after with no exception when not found")]
    [MemberData(nameof(ObserverKeyTypeCases))]
    public async Task TryRemoveAsync_ByKey_Observer_OnNotFound(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = ObserverKeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var result = await repo.TryRemoveAsync([Guid.NewGuid(), Guid.NewGuid()]);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.NotFound);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeFalse();
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var result = await repo.TryRemoveAsync(Guid.NewGuid());
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.NotFound);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeFalse();
        }

        var events = observer.Events.Where(e => e.Operation == "TryRemoveAsync").ToList();
        events.Count.ShouldBe(2);
        events[0].Stage.ShouldBe(ObserverState.Before);
        events[1].Stage.ShouldBe(ObserverState.After);
        events[1].Exception.ShouldBeNull();

        observer.Reset();
    }

    private static IReadOnlyDictionary<string, bool> BuildObserverKeyTypeSpecs()
        => new Dictionary<string, bool>
        {
            ["single-key"] = false,
            ["composite-key"] = true
        };
}
