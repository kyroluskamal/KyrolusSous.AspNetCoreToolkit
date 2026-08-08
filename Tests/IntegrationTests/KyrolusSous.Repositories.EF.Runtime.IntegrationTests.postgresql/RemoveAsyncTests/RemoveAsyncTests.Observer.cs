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

    [Theory(DisplayName = "RemoveRangeAsync emits observer before/after on success")]
    [MemberData(nameof(ObserverKeyTypeCases))]
    public async Task RemoveRangeAsync_Observer_OnSuccess(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = ObserverKeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entities = new List<Review>
            {
                CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "remove-range-observer-composite-1"),
                CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 4, comment: "remove-range-observer-composite-2")
            };
            await SeedReviewsAsync(entities);

            try
            {
                var removed = await repo.RemoveRangeAsync(entities);
                removed.ShouldBeTrue();
                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);

                var events = observer.Events.Where(e => e.Operation == nameof(repo.RemoveRangeAsync)).ToList();
                events.Count.ShouldBe(2);
                events[0].Stage.ShouldBe(ObserverState.Before);
                events[1].Stage.ShouldBe(ObserverState.After);
                events[1].Exception.ShouldBeNull();
            }
            finally
            {
                await CleanupReviewsAsync(entities.Select(x => (x.ProductId, x.CustomerId)));
                observer.Reset();
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var products = new List<Product>
        {
            CreateValidProduct(name: "remove-range-observer-single-1"),
            CreateValidProduct(name: "remove-range-observer-single-2")
        };
        await SeedProductsAsync(products);

        try
        {
            var removed = await singleRepo.RemoveRangeAsync(products);
            removed.ShouldBeTrue();
            (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);

            var events = observer.Events.Where(e => e.Operation == nameof(singleRepo.RemoveRangeAsync)).ToList();
            events.Count.ShouldBe(2);
            events[0].Stage.ShouldBe(ObserverState.Before);
            events[1].Stage.ShouldBe(ObserverState.After);
            events[1].Exception.ShouldBeNull();
        }
        finally
        {
            await CleanupProductsAsync(products.Select(x => x.Id));
            observer.Reset();
        }
    }

    [Theory(DisplayName = "RemoveRangeAsync emits observer after with exception on invalid entities")]
    [MemberData(nameof(ObserverKeyTypeCases))]
    public async Task RemoveRangeAsync_Observer_OnFailure(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = ObserverKeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var observer = scope.ServiceProvider.GetRequiredService<TestRepositoryObserver>();
        observer.Reset();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentNullException>(async () => await repo.RemoveRangeAsync(new Review[] { null! }));
        }
        else
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            await Should.ThrowAsync<ArgumentNullException>(async () => await repo.RemoveRangeAsync(new Product[] { null! }));
        }

        var events = observer.Events.Where(e => e.Operation == "RemoveRangeAsync").ToList();
        events.Count.ShouldBe(2);
        events[0].Stage.ShouldBe(ObserverState.Before);
        events[1].Stage.ShouldBe(ObserverState.After);
        events[1].Exception.ShouldBeOfType<ArgumentNullException>();

        observer.Reset();
    }

    private static IReadOnlyDictionary<string, bool> BuildObserverKeyTypeSpecs()
        => new Dictionary<string, bool>
        {
            ["single-key"] = false,
            ["composite-key"] = true
        };
}
