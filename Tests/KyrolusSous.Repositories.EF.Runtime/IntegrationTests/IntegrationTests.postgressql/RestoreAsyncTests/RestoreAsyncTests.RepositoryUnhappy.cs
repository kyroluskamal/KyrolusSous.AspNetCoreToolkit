namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.RestoreAsyncTests;

public partial class RestoreAsyncTests
{
    [Theory(DisplayName = "RestoreAsync throws for missing entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task RestoreAsync_NotFound_Throws(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<KeyNotFoundException>(async () => await repo.RestoreAsync([Guid.NewGuid(), Guid.NewGuid()]));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<KeyNotFoundException>(async () => await singleRepo.RestoreAsync(Guid.NewGuid()));
    }

    [Theory(DisplayName = "RestoreAsync on active entities returns true")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task RestoreAsync_ActiveEntity_ReturnsTrue(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 3, comment: "active-composite");
            await SeedReviewAsync(entity);

            try
            {
                var restored = await repo.RestoreAsync([entity.ProductId, entity.CustomerId]);
                restored.ShouldBeTrue();
                (await uow.SaveChangesAsync()).ShouldBeGreaterThanOrEqualTo(0);
                (await IsReviewDeletedAsync(entity.ProductId, entity.CustomerId)).ShouldBe(false);
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "active-single");
        await SeedProductAsync(product);

        try
        {
            var restored = await singleRepo.RestoreAsync(product.Id);
            restored.ShouldBeTrue();
            (await uow.SaveChangesAsync()).ShouldBeGreaterThanOrEqualTo(0);
            (await IsProductDeletedAsync(product.Id)).ShouldBe(false);
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "RestoreAsync respects cancellation token")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task RestoreAsync_CanceledToken_Throws(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (compositeKey)
        {
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 4, comment: "cancel-composite");
            await SeedReviewAsync(entity);

            try
            {
                await SoftDeleteReviewAsync(entity.ProductId, entity.CustomerId);
                using var restoreScope = Factory.Services.CreateScope();
                var restoreRepo = restoreScope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                await Should.ThrowAsync<OperationCanceledException>(async () =>
                    await restoreRepo.RestoreAsync([entity.ProductId, entity.CustomerId], cts.Token));
                (await IsReviewDeletedAsync(entity.ProductId, entity.CustomerId)).ShouldBe(true);
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var product = CreateValidProduct(name: "cancel-single");
        await SeedProductAsync(product);

        try
        {
            await SoftDeleteProductAsync(product.Id);
            using var restoreScope = Factory.Services.CreateScope();
            var restoreRepo = restoreScope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await restoreRepo.RestoreAsync(product.Id, cts.Token));
            (await IsProductDeletedAsync(product.Id)).ShouldBe(true);
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    public static TheoryData<string, object?[]?> CompositeInvalidKeyCases => new()
    {
        { "null-keys", null },
        { "empty-keys", Array.Empty<object?>() },
        { "missing-one-key", [DataSeeder.productLaptopId] },
        { "extra-key", [DataSeeder.productLaptopId, DataSeeder.customerJaneId, Guid.NewGuid()] }
    };

    [Theory(DisplayName = "RestoreAsync composite rejects invalid key arrays")]
    [MemberData(nameof(CompositeInvalidKeyCases))]
    public async Task RestoreAsync_Composite_InvalidKeys_Throws(string caseId, object?[]? keyValues)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        await Should.ThrowAsync<ArgumentException>(async () => await repo.RestoreAsync(keyValues));
    }
}
