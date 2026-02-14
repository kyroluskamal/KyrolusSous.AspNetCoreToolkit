namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryRestoreAsyncTests;

public partial class TryRestoreAsyncTests
{
    [Theory(DisplayName = "TryRestoreAsync returns not found for missing entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryRestoreAsync_NotFound_ReturnsNotFound(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var result = await repo.TryRestoreAsync([Guid.NewGuid(), Guid.NewGuid()]);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.NotFound);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeFalse();
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleResult = await singleRepo.TryRestoreAsync(Guid.NewGuid());
        singleResult.Status.ShouldBe(KyrolusRepositoryOperationStatus.NotFound);
        singleResult.Exception.ShouldBeNull();
        singleResult.Value.ShouldBeFalse();
    }

    [Theory(DisplayName = "TryRestoreAsync on active entities returns success")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryRestoreAsync_ActiveEntity_ReturnsSuccess(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 3, comment: "active-composite");
            await SeedReviewAsync(entity);

            try
            {
                var result = await repo.TryRestoreAsync([entity.ProductId, entity.CustomerId]);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                result.Exception.ShouldBeNull();
                result.Value.ShouldBeTrue();

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
            var result = await singleRepo.TryRestoreAsync(product.Id);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeTrue();

            (await uow.SaveChangesAsync()).ShouldBeGreaterThanOrEqualTo(0);
            (await IsProductDeletedAsync(product.Id)).ShouldBe(false);
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "TryRestoreAsync respects cancellation token")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryRestoreAsync_CanceledToken_ReturnsFailed(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (compositeKey)
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 4, comment: "cancel-composite");
            await SeedReviewAsync(entity);

            try
            {
                await SoftDeleteReviewAsync(entity.ProductId, entity.CustomerId);
                var result = await repo.TryRestoreAsync([entity.ProductId, entity.CustomerId], cts.Token);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
                result.Exception.ShouldBeOfType<OperationCanceledException>();
                (await IsReviewDeletedAsync(entity.ProductId, entity.CustomerId)).ShouldBe(true);
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        using var singleScope = Factory.Services.CreateScope();
        var singleRepo = singleScope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "cancel-single");
        await SeedProductAsync(product);

        try
        {
            await SoftDeleteProductAsync(product.Id);
            var result = await singleRepo.TryRestoreAsync(product.Id, cts.Token);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
            result.Exception.ShouldBeOfType<OperationCanceledException>();
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

    [Theory(DisplayName = "TryRestoreAsync composite rejects invalid key arrays")]
    [MemberData(nameof(CompositeInvalidKeyCases))]
    public async Task TryRestoreAsync_Composite_InvalidKeys_Throws(string caseId, object?[]? keyValues)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        await Should.ThrowAsync<ArgumentException>(async () => await repo.TryRestoreAsync(keyValues));
    }
}
