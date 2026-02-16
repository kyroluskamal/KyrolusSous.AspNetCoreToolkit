namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.RemoveAsyncTests;

public partial class RemoveAsyncTests
{
    private static readonly IReadOnlyDictionary<string, bool> ObserverKeyTypeSpecs = BuildObserverKeyTypeSpecs();
    public static TheoryData<string> ObserverKeyTypeCases => CaseIdsFrom(ObserverKeyTypeSpecs);

    [Theory(DisplayName = "RemoveAsync by key emits observer before/after on success")]
    [MemberData(nameof(ObserverKeyTypeCases))]
    public async Task RemoveAsync_ByKey_Observer_OnSuccess(string caseId)
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
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 3, comment: "remove-observer-composite");
            await SeedReviewAsync(entity);

            try
            {
                var removed = await repo.RemoveAsync([entity.ProductId, entity.CustomerId]);
                removed.ShouldBeTrue();
                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);

                var events = observer.Events.Where(e => e.Operation == nameof(repo.RemoveAsync)).ToList();
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
        var product = CreateValidProduct(name: "remove-observer-single");
        await SeedProductAsync(product);

        try
        {
            var removed = await singleRepo.RemoveAsync(product.Id);
            removed.ShouldBeTrue();
            (await singleUow.SaveChangesAsync()).ShouldBeGreaterThan(0);

            var events = observer.Events.Where(e => e.Operation == nameof(singleRepo.RemoveAsync)).ToList();
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

    [Theory(DisplayName = "RemoveAsync by key emits observer after with exception when entity is missing")]
    [MemberData(nameof(ObserverKeyTypeCases))]
    public async Task RemoveAsync_ByKey_Observer_OnNotFound(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = ObserverKeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<KeyNotFoundException>(async () => await repo.RemoveAsync([Guid.NewGuid(), Guid.NewGuid()]));
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            await Should.ThrowAsync<KeyNotFoundException>(async () => await repo.RemoveAsync(Guid.NewGuid()));
        }

        var events = observer.Events.Where(e => e.Operation == "RemoveAsync").ToList();
        events.Count.ShouldBe(2);
        events[0].Stage.ShouldBe(ObserverState.Before);
        events[1].Stage.ShouldBe(ObserverState.After);
        events[1].Exception.ShouldBeOfType<KeyNotFoundException>();

        observer.Reset();
    }

    private static IReadOnlyDictionary<string, bool> BuildObserverKeyTypeSpecs()
        => new Dictionary<string, bool>
        {
            ["single-key"] = false,
            ["composite-key"] = true
        };
}
