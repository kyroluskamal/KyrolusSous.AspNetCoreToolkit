namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryRemoveAsyncTests;

public partial class TryRemoveAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private sealed record TryRemoveSingleApiSpec(Func<Product> SeedEntity, bool SoftDeleteFlag);
    private sealed record TryRemoveCompositeApiSpec(Func<Review> SeedEntity, bool SoftDeleteFlag);

    private static readonly IReadOnlyDictionary<string, bool> KeyTypeSpecs = BuildKeyTypeSpecs();
    private static readonly IReadOnlyDictionary<string, TryRemoveSingleApiSpec> SingleApiSuccessSpecs = BuildSingleApiSuccessSpecs();
    private static readonly IReadOnlyDictionary<string, TryRemoveCompositeApiSpec> CompositeApiSuccessSpecs = BuildCompositeApiSuccessSpecs();

    public static TheoryData<string> KeyTypeCases => CaseIdsFrom(KeyTypeSpecs);
    public static TheoryData<string> SingleApiSuccessCases => CaseIdsFrom(SingleApiSuccessSpecs);
    public static TheoryData<string> CompositeApiSuccessCases => CaseIdsFrom(CompositeApiSuccessSpecs);

    [Theory(DisplayName = "TryRemoveAsync by entity removes entities after SaveChanges")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryRemoveAsync_ByEntity_AfterSave_Removes(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "try-remove-entity-composite");
            await SeedReviewAsync(entity);

            try
            {
                var result = await repo.TryRemoveAsync(entity);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                result.Exception.ShouldBeNull();
                result.Value.ShouldBeTrue();

                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
                (await ReviewExistsAsync(entity.ProductId, entity.CustomerId)).ShouldBeFalse();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "try-remove-entity-single");
        await SeedProductAsync(product);

        try
        {
            var result = await singleRepo.TryRemoveAsync(product);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeTrue();

            (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
            (await ProductExistsAsync(product.Id)).ShouldBeFalse();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "TryRemoveAsync by key removes entities after SaveChanges")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryRemoveAsync_ByKey_AfterSave_Removes(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 3, comment: "try-remove-key-composite");
            await SeedReviewAsync(entity);

            try
            {
                var result = await repo.TryRemoveAsync([entity.ProductId, entity.CustomerId]);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                result.Exception.ShouldBeNull();
                result.Value.ShouldBeTrue();

                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
                (await ReviewExistsAsync(entity.ProductId, entity.CustomerId)).ShouldBeFalse();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "try-remove-key-single");
        await SeedProductAsync(product);

        try
        {
            var result = await singleRepo.TryRemoveAsync(product.Id);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeTrue();

            (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
            (await ProductExistsAsync(product.Id)).ShouldBeFalse();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "TryRemoveAsync by key does not persist without SaveChanges")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryRemoveAsync_ByKey_WithoutSaveChanges_DoesNotPersist(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];

        if (compositeKey)
        {
            var entity = CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 1, comment: "try-remove-nosave-composite");
            await SeedReviewAsync(entity);

            try
            {
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var result = await repo.TryRemoveAsync([entity.ProductId, entity.CustomerId]);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                result.Exception.ShouldBeNull();
                result.Value.ShouldBeTrue();

                (await ReviewExistsAsync(entity.ProductId, entity.CustomerId)).ShouldBeTrue();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var product = CreateValidProduct(name: "try-remove-nosave-single");
        await SeedProductAsync(product);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var result = await repo.TryRemoveAsync(product.Id);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeTrue();

            (await ProductExistsAsync(product.Id)).ShouldBeTrue();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "TryRemoveAsync by key can hard-delete soft-deleted entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryRemoveAsync_ByKey_SoftDeletedEntity_AfterSave_RemovesRow(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        if (compositeKey)
        {
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 5, comment: "try-remove-soft-composite");
            await SeedReviewAsync(entity);

            try
            {
                await SoftDeleteReviewAsync(entity.ProductId, entity.CustomerId);

                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var result = await repo.TryRemoveAsync([entity.ProductId, entity.CustomerId]);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                result.Exception.ShouldBeNull();
                result.Value.ShouldBeTrue();

                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
                (await ReviewExistsAsync(entity.ProductId, entity.CustomerId)).ShouldBeFalse();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var product = CreateValidProduct(name: "try-remove-soft-single");
        await SeedProductAsync(product);

        try
        {
            await SoftDeleteProductAsync(product.Id);

            var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var result = await singleRepo.TryRemoveAsync(product.Id);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeTrue();

            (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
            (await ProductExistsAsync(product.Id)).ShouldBeFalse();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "TryRemoveAsync API deletes single-key entities")]
    [MemberData(nameof(SingleApiSuccessCases))]
    public async Task TryRemoveAsync_Api_SingleKey_DeletesEntity(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SingleApiSuccessSpecs[caseId];
        var entity = spec.SeedEntity();
        await SeedProductAsync(entity);

        try
        {
            var (response, content) = await DeleteSingleTryAsync<Product>(entity.Id, spec.SoftDeleteFlag);
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent, content);
            content.ShouldBeEmpty();

            (await ProductExistsAsync(entity.Id)).ShouldBeFalse();
        }
        finally
        {
            await CleanupProductAsync(entity.Id);
        }
    }

    [Theory(DisplayName = "TryRemoveAsync API deletes composite-key entities")]
    [MemberData(nameof(CompositeApiSuccessCases))]
    public async Task TryRemoveAsync_Api_CompositeKey_DeletesEntity(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = CompositeApiSuccessSpecs[caseId];
        var entity = spec.SeedEntity();
        await SeedReviewAsync(entity);

        try
        {
            var (response, content) = await DeleteCompositeTryAsync<Review>([entity.ProductId, entity.CustomerId], spec.SoftDeleteFlag);
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent, content);
            content.ShouldBeEmpty();

            (await ReviewExistsAsync(entity.ProductId, entity.CustomerId)).ShouldBeFalse();
        }
        finally
        {
            await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
        }
    }

    private static IReadOnlyDictionary<string, bool> BuildKeyTypeSpecs()
        => new Dictionary<string, bool>
        {
            ["single-key"] = false,
            ["composite-key"] = true
        };

    private static IReadOnlyDictionary<string, TryRemoveSingleApiSpec> BuildSingleApiSuccessSpecs()
        => new Dictionary<string, TryRemoveSingleApiSpec>
        {
            ["single-softdelete-false"] = new(
                SeedEntity: () => CreateValidProduct(name: "try-remove-api-single-false"),
                SoftDeleteFlag: false),
            ["single-softdelete-true"] = new(
                SeedEntity: () => CreateValidProduct(name: "try-remove-api-single-true"),
                SoftDeleteFlag: true)
        };

    private static IReadOnlyDictionary<string, TryRemoveCompositeApiSpec> BuildCompositeApiSuccessSpecs()
        => new Dictionary<string, TryRemoveCompositeApiSpec>
        {
            ["composite-softdelete-false"] = new(
                SeedEntity: () => CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 4, comment: "try-remove-api-composite-false"),
                SoftDeleteFlag: false),
            ["composite-softdelete-true"] = new(
                SeedEntity: () => CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 5, comment: "try-remove-api-composite-true"),
                SoftDeleteFlag: true)
        };
}
