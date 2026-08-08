namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.UpdateAsyncTests;

public partial class UpdateAsyncTests
{
    public static TheoryData<string, bool> NullEntityCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "UpdateAsync rejects null entities")]
    [MemberData(nameof(NullEntityCases))]
    public async Task UpdateAsync_NullEntity_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentNullException>(async () => await repo.UpdateAsync(null!, default));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentNullException>(async () => await singleRepo.UpdateAsync(null!, default));
    }

    public static TheoryData<string, bool> NotFoundCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "UpdateAsync throws when entity is not found")]
    [MemberData(nameof(NotFoundCases))]
    public async Task UpdateAsync_NotFound_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(Guid.NewGuid(), DataSeeder.customerJohnId, rating: 2);
            await Should.ThrowAsync<KeyNotFoundException>(async () => await repo.UpdateAsync(entity));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(id: Guid.NewGuid(), name: "missing");
        await Should.ThrowAsync<KeyNotFoundException>(async () => await singleRepo.UpdateAsync(product));
    }

    public static TheoryData<string, bool> SaveFailureCases => new()
    {
        { "single-key-duplicate-sku", false },
        { "single-key-invalid-store", true }
    };

    [Theory(DisplayName = "UpdateAsync save failures throw DbUpdateException")]
    [MemberData(nameof(SaveFailureCases))]
    public async Task UpdateAsync_SaveFailure_ThrowsDbUpdateException(string caseId, bool invalidStore)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var seedProduct = CreateValidProduct();
        await SeedProductAsync(seedProduct);

        try
        {
            var updated = Clone(seedProduct);
            if (invalidStore)
            {
                updated.StoreId = Guid.NewGuid();
            }
            else
            {
                updated.Sku = "LP-15";
            }

            await singleRepo.UpdateAsync(updated);
            await Should.ThrowAsync<DbUpdateException>(async () => await uow.SaveChangesAsync());
        }
        finally
        {
            await CleanupProductAsync(seedProduct.Id);
        }
    }

    public static TheoryData<string, bool> WithoutSaveCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "UpdateAsync does not persist without SaveChanges")]
    [MemberData(nameof(WithoutSaveCases))]
    public async Task UpdateAsync_WithoutSaveChanges_DoesNotPersist(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var seed = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "Before");
            await SeedReviewAsync(seed);

            try
            {
                var updated = Clone(seed);
                updated.Comment = "After";
                await repo.UpdateAsync(updated);

                using var verifyScope = Factory.Services.CreateScope();
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var persisted = await verifyDb.Reviews.AsNoTracking()
                    .SingleAsync(x => x.ProductId == seed.ProductId && x.CustomerId == seed.CustomerId);
                persisted.Comment.ShouldBe("Before");
            }
            finally
            {
                await CleanupReviewAsync(seed.ProductId, seed.CustomerId);
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "Before");
        await SeedProductAsync(product);

        try
        {
            var updated = Clone(product);
            updated.Name = "After";
            await singleRepo.UpdateAsync(updated);

            using var verifyScope = Factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await verifyDb.Products.AsNoTracking().SingleAsync(x => x.Id == product.Id);
            persisted.Name.ShouldBe("Before");
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    public static TheoryData<string, bool> CancellationCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "UpdateAsync respects cancellation token")]
    [MemberData(nameof(CancellationCases))]
    public async Task UpdateAsync_CanceledToken_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJaneId, rating: 5);
            await Should.ThrowAsync<OperationCanceledException>(async () => await repo.UpdateAsync(entity, cts.Token));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(id: DataSeeder.productLaptopId);
        await Should.ThrowAsync<OperationCanceledException>(async () => await singleRepo.UpdateAsync(product, cts.Token));
    }
}
