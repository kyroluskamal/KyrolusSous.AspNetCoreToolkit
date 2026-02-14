namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryRestoreAsyncTests;

public partial class TryRestoreAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    [Theory(DisplayName = "TryRestoreAsync restores soft-deleted entities after SaveChanges")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryRestoreAsync_AfterSave_RestoresEntity(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 3, comment: "restore-composite");
            await SeedReviewAsync(entity);

            try
            {
                await SoftDeleteReviewAsync(entity.ProductId, entity.CustomerId);
                (await IsReviewDeletedAsync(entity.ProductId, entity.CustomerId)).ShouldBe(true);

                var result = await repo.TryRestoreAsync([entity.ProductId, entity.CustomerId]);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                result.Exception.ShouldBeNull();
                result.Value.ShouldBeTrue();

                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
                (await IsReviewDeletedAsync(entity.ProductId, entity.CustomerId)).ShouldBe(false);
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "restore-single");
        await SeedProductAsync(product);

        try
        {
            await SoftDeleteProductAsync(product.Id);
            (await IsProductDeletedAsync(product.Id)).ShouldBe(true);

            var result = await singleRepo.TryRestoreAsync(product.Id);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeTrue();

            (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
            (await IsProductDeletedAsync(product.Id)).ShouldBe(false);
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "TryRestoreAsync does not persist without SaveChanges")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryRestoreAsync_WithoutSaveChanges_DoesNotPersist(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 2, comment: "restore-without-save");
            await SeedReviewAsync(entity);

            try
            {
                await SoftDeleteReviewAsync(entity.ProductId, entity.CustomerId);
                var result = await repo.TryRestoreAsync([entity.ProductId, entity.CustomerId]);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);

                (await IsReviewDeletedAsync(entity.ProductId, entity.CustomerId)).ShouldBe(true);
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "restore-without-save-single");
        await SeedProductAsync(product);

        try
        {
            await SoftDeleteProductAsync(product.Id);
            var result = await singleRepo.TryRestoreAsync(product.Id);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);

            (await IsProductDeletedAsync(product.Id)).ShouldBe(true);
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }
}
