namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryUpdateAsyncTests;

public partial class TryUpdateAsyncTests
{
    private static readonly IReadOnlyDictionary<string, bool> KeyTypeSpecs = BuildKeyTypeSpecs();
    public static TheoryData<string> KeyTypeCases => CaseIdsFrom(KeyTypeSpecs);

    [Theory(DisplayName = "TryUpdateAsync rejects null entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryUpdateAsync_NullEntity_Throws(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentNullException>(async () => await repo.TryUpdateAsync((Review)null!, default));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentNullException>(async () => await singleRepo.TryUpdateAsync((Product)null!, default));
    }

    [Theory(DisplayName = "TryUpdateAsync returns failed when entity is not found")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryUpdateAsync_NotFound_ReturnsFailedWithKeyNotFound(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var missingEntity = CreateValidReview(Guid.NewGuid(), Guid.NewGuid(), rating: 1, comment: "missing");
            var result = await repo.TryUpdateAsync(missingEntity);

            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
            result.Exception.ShouldBeOfType<KeyNotFoundException>();
            result.Value.ShouldBeNull();
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var missingProduct = CreateValidProduct(id: Guid.NewGuid(), name: "missing");
        var singleResult = await singleRepo.TryUpdateAsync(missingProduct);

        singleResult.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
        singleResult.Exception.ShouldBeOfType<KeyNotFoundException>();
        singleResult.Value.ShouldBeNull();
    }

    [Theory(DisplayName = "TryUpdateAsync does not persist without SaveChanges")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryUpdateAsync_WithoutSaveChanges_DoesNotPersist(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];

        if (compositeKey)
        {
            var seed = CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 2, comment: "Before");
            await SeedReviewAsync(seed);

            try
            {
                var updated = Clone(seed);
                updated.Comment = "After";

                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var result = await repo.TryUpdateAsync(updated);

                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                result.Exception.ShouldBeNull();
                result.Value.ShouldNotBeNull();

                var persisted = await FindReviewAsync(seed.ProductId, seed.CustomerId);
                persisted.ShouldNotBeNull();
                persisted!.Comment.ShouldBe("Before");
            }
            finally
            {
                await CleanupReviewAsync(seed.ProductId, seed.CustomerId);
            }

            return;
        }

        var product = CreateValidProduct(name: "Before");
        await SeedProductAsync(product);

        try
        {
            var updated = Clone(product);
            updated.Name = "After";

            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var result = await repo.TryUpdateAsync(updated);

            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            result.Exception.ShouldBeNull();
            result.Value.ShouldNotBeNull();

            var persisted = await FindProductAsync(product.Id);
            persisted.ShouldNotBeNull();
            persisted!.Name.ShouldBe("Before");
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "TryUpdateAsync returns failed for canceled tokens")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryUpdateAsync_CanceledToken_ReturnsFailed(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (compositeKey)
        {
            var seed = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 3, comment: "BeforeCancel");
            await SeedReviewAsync(seed);

            try
            {
                var updated = Clone(seed);
                updated.Comment = "AfterCancel";

                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var result = await repo.TryUpdateAsync(updated, cts.Token);

                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
                result.Exception.ShouldBeOfType<OperationCanceledException>();
                result.Value.ShouldBeNull();

                var persisted = await FindReviewAsync(seed.ProductId, seed.CustomerId);
                persisted.ShouldNotBeNull();
                persisted!.Comment.ShouldBe("BeforeCancel");
            }
            finally
            {
                await CleanupReviewAsync(seed.ProductId, seed.CustomerId);
            }

            return;
        }

        var product = CreateValidProduct(name: "BeforeCancel");
        await SeedProductAsync(product);

        try
        {
            var updated = Clone(product);
            updated.Name = "AfterCancel";

            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var result = await repo.TryUpdateAsync(updated, cts.Token);

            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
            result.Exception.ShouldBeOfType<OperationCanceledException>();
            result.Value.ShouldBeNull();

            var persisted = await FindProductAsync(product.Id);
            persisted.ShouldNotBeNull();
            persisted!.Name.ShouldBe("BeforeCancel");
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "TryUpdateAsync returns failed for soft-deleted entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryUpdateAsync_SoftDeletedEntity_ReturnsFailedWithKeyNotFound(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];

        if (compositeKey)
        {
            var seed = CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 3, comment: "BeforeDelete");
            await SeedReviewAsync(seed);

            try
            {
                await SoftDeleteReviewAsync(seed.ProductId, seed.CustomerId);
                var updated = Clone(seed);
                updated.Comment = "AfterDelete";

                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var result = await repo.TryUpdateAsync(updated);

                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
                result.Exception.ShouldBeOfType<KeyNotFoundException>();
                result.Value.ShouldBeNull();
            }
            finally
            {
                await CleanupReviewAsync(seed.ProductId, seed.CustomerId);
            }

            return;
        }

        var product = CreateValidProduct(name: "BeforeDelete");
        await SeedProductAsync(product);

        try
        {
            await SoftDeleteProductAsync(product.Id);
            var updated = Clone(product);
            updated.Name = "AfterDelete";

            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var result = await repo.TryUpdateAsync(updated);

            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
            result.Exception.ShouldBeOfType<KeyNotFoundException>();
            result.Value.ShouldBeNull();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Fact(DisplayName = "TryUpdateAsync save failure surfaces on SaveChanges")]
    public async Task TryUpdateAsync_SaveFailure_ThrowsOnSaveChanges()
    {
        var seed = CreateValidProduct(name: "TryUpdateSaveFailure");
        await SeedProductAsync(seed);

        try
        {
            var updated = Clone(seed);
            updated.StoreId = Guid.NewGuid();

            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
            var result = await repo.TryUpdateAsync(updated);

            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            result.Exception.ShouldBeNull();
            result.Value.ShouldNotBeNull();

            await Should.ThrowAsync<DbUpdateException>(async () => await uow.SaveChangesAsync());
        }
        finally
        {
            await CleanupProductAsync(seed.Id);
        }
    }

    private static IReadOnlyDictionary<string, bool> BuildKeyTypeSpecs()
        => new Dictionary<string, bool>
        {
            ["single-key"] = false,
            ["composite-key"] = true
        };
}
