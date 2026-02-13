namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.RemoveAsyncTests;

public partial class RemoveAsyncTests
{
    private static readonly IReadOnlyDictionary<string, bool> KeyTypeSpecs = BuildKeyTypeSpecs();

    public static TheoryData<string> KeyTypeCases => CaseIdsFrom(KeyTypeSpecs);

    [Theory(DisplayName = "RemoveAsync rejects null entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task RemoveAsync_NullEntity_Throws(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentNullException>(async () => await repo.RemoveAsync((Review)null!, default));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentNullException>(async () => await singleRepo.RemoveAsync((Product)null!, default));
    }

    [Theory(DisplayName = "RemoveAsync by key throws when entity is not found")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task RemoveAsync_ByKey_NotFound_Throws(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<KeyNotFoundException>(async () => await repo.RemoveAsync([Guid.NewGuid(), Guid.NewGuid()]));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<KeyNotFoundException>(async () => await singleRepo.RemoveAsync(Guid.NewGuid()));
    }

    [Theory(DisplayName = "TryRemoveAsync by key returns not found for missing entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryRemoveAsync_ByKey_NotFound_ReturnsNotFound(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var result = await repo.TryRemoveAsync([Guid.NewGuid(), Guid.NewGuid()]);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.NotFound);
            result.Exception.ShouldBeNull();
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleResult = await singleRepo.TryRemoveAsync(Guid.NewGuid());
        singleResult.Status.ShouldBe(KyrolusRepositoryOperationStatus.NotFound);
        singleResult.Exception.ShouldBeNull();
    }

    [Theory(DisplayName = "RemoveAsync by key does not persist without SaveChanges")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task RemoveAsync_ByKey_WithoutSaveChanges_DoesNotPersist(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "without-save");
            await SeedReviewAsync(entity);

            try
            {
                await repo.RemoveAsync([entity.ProductId, entity.CustomerId]);
                var exists = await ReviewExistsAsync(entity.ProductId, entity.CustomerId);
                exists.ShouldBeTrue();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "remove-without-save");
        await SeedProductAsync(product);

        try
        {
            await singleRepo.RemoveAsync(product.Id);
            var exists = await ProductExistsAsync(product.Id);
            exists.ShouldBeTrue();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "RemoveAsync entity overload removes entity after SaveChanges")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task RemoveAsync_EntityOverload_AfterSave_Removes(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 1, comment: "remove-entity-overload");
            await SeedReviewAsync(entity);

            try
            {
                var removed = await repo.RemoveAsync(entity);
                removed.ShouldBeTrue();
                var saved = await uow.SaveChangesAsync();
                saved.ShouldBeGreaterThan(0);

                var exists = await ReviewExistsAsync(entity.ProductId, entity.CustomerId);
                exists.ShouldBeFalse();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "remove-entity-overload");
        await SeedProductAsync(product);

        try
        {
            var removed = await singleRepo.RemoveAsync(product);
            removed.ShouldBeTrue();
            var saved = await uow.SaveChangesAsync();
            saved.ShouldBeGreaterThan(0);

            var exists = await ProductExistsAsync(product.Id);
            exists.ShouldBeFalse();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "RemoveRangeAsync returns true for empty sequences")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task RemoveRangeAsync_EmptySequence_ReturnsTrue(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var removed = await repo.RemoveRangeAsync(Array.Empty<Review>());
            removed.ShouldBeTrue();
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleRemoved = await singleRepo.RemoveRangeAsync(Array.Empty<Product>());
        singleRemoved.ShouldBeTrue();
    }

    [Theory(DisplayName = "RemoveRangeAsync removes entities after SaveChanges")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task RemoveRangeAsync_AfterSave_RemovesEntities(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entities = new List<Review>
            {
                CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "remove-range-repo-1"),
                CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 5, comment: "remove-range-repo-2")
            };
            await SeedReviewsAsync(entities);

            try
            {
                var removed = await repo.RemoveRangeAsync(entities);
                removed.ShouldBeTrue();
                var saved = await uow.SaveChangesAsync();
                saved.ShouldBeGreaterThan(0);

                foreach (var entity in entities)
                    (await ReviewExistsAsync(entity.ProductId, entity.CustomerId)).ShouldBeFalse();
            }
            finally
            {
                await CleanupReviewsAsync(entities.Select(x => (x.ProductId, x.CustomerId)));
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var products = new List<Product>
        {
            CreateValidProduct(name: "remove-range-repo-single-1"),
            CreateValidProduct(name: "remove-range-repo-single-2")
        };
        await SeedProductsAsync(products);

        try
        {
            var removed = await singleRepo.RemoveRangeAsync(products);
            removed.ShouldBeTrue();
            var saved = await uow.SaveChangesAsync();
            saved.ShouldBeGreaterThan(0);

            foreach (var product in products)
                (await ProductExistsAsync(product.Id)).ShouldBeFalse();
        }
        finally
        {
            await CleanupProductsAsync(products.Select(x => x.Id));
        }
    }

    [Theory(DisplayName = "RemoveAsync respects cancellation token")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task RemoveAsync_CanceledToken_Throws(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 4, comment: "remove-canceled");
            await SeedReviewAsync(entity);

            try
            {
                await Should.ThrowAsync<OperationCanceledException>(async () =>
                    await repo.RemoveAsync([entity.ProductId, entity.CustomerId], cts.Token));
                (await ReviewExistsAsync(entity.ProductId, entity.CustomerId)).ShouldBeTrue();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "remove-canceled-single");
        await SeedProductAsync(product);

        try
        {
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await singleRepo.RemoveAsync(product.Id, cts.Token));
            (await ProductExistsAsync(product.Id)).ShouldBeTrue();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    private static IReadOnlyDictionary<string, bool> BuildKeyTypeSpecs()
        => new Dictionary<string, bool>
        {
            ["single-key"] = false,
            ["composite-key"] = true
        };
}
