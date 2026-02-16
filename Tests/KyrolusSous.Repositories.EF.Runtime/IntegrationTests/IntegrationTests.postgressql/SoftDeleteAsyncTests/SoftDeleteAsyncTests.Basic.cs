namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.SoftDeleteAsyncTests;

public partial class SoftDeleteAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    [Theory(DisplayName = "SoftDeleteAsync marks entity as deleted after SaveChanges")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task SoftDeleteAsync_AfterSave_MarksDeleted(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 4, comment: "soft-delete-composite");
            await SeedReviewAsync(entity);

            try
            {
                var deleted = await repo.SoftDeleteAsync([entity.ProductId, entity.CustomerId]);
                deleted.ShouldBeTrue();

                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
                (await IsReviewDeletedAsync(entity.ProductId, entity.CustomerId)).ShouldBe(true);
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "soft-delete-single");
        await SeedProductAsync(product);

        try
        {
            var deleted = await singleRepo.SoftDeleteAsync(product.Id);
            deleted.ShouldBeTrue();

            (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
            (await IsProductDeletedAsync(product.Id)).ShouldBe(true);
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "SoftDeleteAsync does not persist without SaveChanges")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task SoftDeleteAsync_WithoutSaveChanges_DoesNotPersist(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 2, comment: "without-save");
            await SeedReviewAsync(entity);

            try
            {
                var deleted = await repo.SoftDeleteAsync([entity.ProductId, entity.CustomerId]);
                deleted.ShouldBeTrue();

                (await IsReviewDeletedAsync(entity.ProductId, entity.CustomerId)).ShouldBe(false);
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "without-save-single");
        await SeedProductAsync(product);

        try
        {
            var deleted = await singleRepo.SoftDeleteAsync(product.Id);
            deleted.ShouldBeTrue();

            (await IsProductDeletedAsync(product.Id)).ShouldBe(false);
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }
}
