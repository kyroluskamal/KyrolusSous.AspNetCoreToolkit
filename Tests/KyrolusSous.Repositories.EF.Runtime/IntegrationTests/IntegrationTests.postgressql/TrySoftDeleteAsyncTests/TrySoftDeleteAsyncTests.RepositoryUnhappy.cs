namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TrySoftDeleteAsyncTests;

public partial class TrySoftDeleteAsyncTests
{
    [Theory(DisplayName = "TrySoftDeleteAsync returns not found for missing entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TrySoftDeleteAsync_NotFound_ReturnsNotFound(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var result = await repo.TrySoftDeleteAsync([Guid.NewGuid(), Guid.NewGuid()]);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.NotFound);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeFalse();
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleResult = await singleRepo.TrySoftDeleteAsync(Guid.NewGuid());
        singleResult.Status.ShouldBe(KyrolusRepositoryOperationStatus.NotFound);
        singleResult.Exception.ShouldBeNull();
        singleResult.Value.ShouldBeFalse();
    }

    [Theory(DisplayName = "TrySoftDeleteAsync returns not found for already deleted entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TrySoftDeleteAsync_AlreadyDeleted_ReturnsNotFound(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 5, comment: "already-deleted");
            await SeedReviewAsync(entity);

            try
            {
                (await repo.TrySoftDeleteAsync([entity.ProductId, entity.CustomerId])).Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);

                var second = await repo.TrySoftDeleteAsync([entity.ProductId, entity.CustomerId]);
                second.Status.ShouldBe(KyrolusRepositoryOperationStatus.NotFound);
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "already-deleted-single");
        await SeedProductAsync(product);

        try
        {
            (await singleRepo.TrySoftDeleteAsync(product.Id)).Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);

            var second = await singleRepo.TrySoftDeleteAsync(product.Id);
            second.Status.ShouldBe(KyrolusRepositoryOperationStatus.NotFound);
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "TrySoftDeleteAsync respects cancellation token")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TrySoftDeleteAsync_CanceledToken_ReturnsFailed(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (compositeKey)
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 3, comment: "canceled");
            await SeedReviewAsync(entity);

            try
            {
                var result = await repo.TrySoftDeleteAsync([entity.ProductId, entity.CustomerId], cts.Token);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
                result.Exception.ShouldBeOfType<OperationCanceledException>();
                (await IsReviewDeletedAsync(entity.ProductId, entity.CustomerId)).ShouldBe(false);
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        using var singleScope = Factory.Services.CreateScope();
        var singleRepo = singleScope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "canceled-single");
        await SeedProductAsync(product);

        try
        {
            var result = await singleRepo.TrySoftDeleteAsync(product.Id, cts.Token);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
            result.Exception.ShouldBeOfType<OperationCanceledException>();
            (await IsProductDeletedAsync(product.Id)).ShouldBe(false);
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

    [Theory(DisplayName = "TrySoftDeleteAsync composite rejects invalid key arrays")]
    [MemberData(nameof(CompositeInvalidKeyCases))]
    public async Task TrySoftDeleteAsync_Composite_InvalidKeys_Throws(string caseId, object?[]? keys)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        await Should.ThrowAsync<ArgumentException>(async () => await repo.TrySoftDeleteAsync(keys));
    }
}
