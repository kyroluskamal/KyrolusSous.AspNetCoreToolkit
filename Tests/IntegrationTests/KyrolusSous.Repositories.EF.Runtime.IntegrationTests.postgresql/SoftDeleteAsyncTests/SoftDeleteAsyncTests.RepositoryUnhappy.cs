namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.SoftDeleteAsyncTests;

public partial class SoftDeleteAsyncTests
{
    [Theory(DisplayName = "SoftDeleteAsync throws for missing entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task SoftDeleteAsync_NotFound_Throws(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<KeyNotFoundException>(async () => await repo.SoftDeleteAsync([Guid.NewGuid(), Guid.NewGuid()]));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<KeyNotFoundException>(async () => await singleRepo.SoftDeleteAsync(Guid.NewGuid()));
    }

    [Theory(DisplayName = "SoftDeleteAsync throws for already deleted entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task SoftDeleteAsync_AlreadyDeleted_Throws(string caseId)
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
                (await repo.SoftDeleteAsync([entity.ProductId, entity.CustomerId])).ShouldBeTrue();
                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);

                await Should.ThrowAsync<KeyNotFoundException>(async () =>
                    await repo.SoftDeleteAsync([entity.ProductId, entity.CustomerId]));
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
            (await singleRepo.SoftDeleteAsync(product.Id)).ShouldBeTrue();
            (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);

            await Should.ThrowAsync<KeyNotFoundException>(async () =>
                await singleRepo.SoftDeleteAsync(product.Id));
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "SoftDeleteAsync respects cancellation token")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task SoftDeleteAsync_CanceledToken_Throws(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (compositeKey)
        {
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 3, comment: "canceled");
            await SeedReviewAsync(entity);

            try
            {
                using var cancelScope = Factory.Services.CreateScope();
                var repo = cancelScope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                await Should.ThrowAsync<OperationCanceledException>(async () =>
                    await repo.SoftDeleteAsync([entity.ProductId, entity.CustomerId], cts.Token));
                (await IsReviewDeletedAsync(entity.ProductId, entity.CustomerId)).ShouldBe(false);
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var product = CreateValidProduct(name: "canceled-single");
        await SeedProductAsync(product);

        try
        {
            using var cancelScope = Factory.Services.CreateScope();
            var repo = cancelScope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await repo.SoftDeleteAsync(product.Id, cts.Token));
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

    [Theory(DisplayName = "SoftDeleteAsync composite rejects invalid key arrays")]
    [MemberData(nameof(CompositeInvalidKeyCases))]
    public async Task SoftDeleteAsync_Composite_InvalidKeys_Throws(string caseId, object?[]? keys)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        await Should.ThrowAsync<ArgumentException>(async () => await repo.SoftDeleteAsync(keys));
    }
}
