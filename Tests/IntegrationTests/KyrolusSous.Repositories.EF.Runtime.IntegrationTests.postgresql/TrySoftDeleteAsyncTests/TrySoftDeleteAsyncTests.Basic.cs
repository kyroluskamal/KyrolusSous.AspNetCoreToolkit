namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TrySoftDeleteAsyncTests;

public partial class TrySoftDeleteAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private static readonly IReadOnlyDictionary<string, bool> KeyTypeSpecs = BuildKeyTypeSpecs();
    public static TheoryData<string> KeyTypeCases => CaseIdsFrom(KeyTypeSpecs);

    [Theory(DisplayName = "TrySoftDeleteAsync marks entity as deleted after SaveChanges")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TrySoftDeleteAsync_AfterSave_MarksDeleted(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 4, comment: "try-soft-composite");
            await SeedReviewAsync(entity);

            try
            {
                var result = await repo.TrySoftDeleteAsync([entity.ProductId, entity.CustomerId]);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                result.Exception.ShouldBeNull();
                result.Value.ShouldBeTrue();

                var saved = await uow.SaveChangesAsync();
                saved.ShouldBeGreaterThan(0);

                (await ReviewExistsAsync(entity.ProductId, entity.CustomerId)).ShouldBeTrue();
                (await IsReviewDeletedAsync(entity.ProductId, entity.CustomerId)).ShouldBe(true);
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "try-soft-single");
        await SeedProductAsync(product);

        try
        {
            var result = await singleRepo.TrySoftDeleteAsync(product.Id);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeTrue();

            var saved = await uow.SaveChangesAsync();
            saved.ShouldBeGreaterThan(0);

            (await ProductExistsAsync(product.Id)).ShouldBeTrue();
            (await IsProductDeletedAsync(product.Id)).ShouldBe(true);
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "TrySoftDeleteAsync does not persist without SaveChanges")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TrySoftDeleteAsync_WithoutSaveChanges_DoesNotPersist(string caseId)
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
                var result = await repo.TrySoftDeleteAsync([entity.ProductId, entity.CustomerId]);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);

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
            var result = await singleRepo.TrySoftDeleteAsync(product.Id);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);

            (await IsProductDeletedAsync(product.Id)).ShouldBe(false);
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
