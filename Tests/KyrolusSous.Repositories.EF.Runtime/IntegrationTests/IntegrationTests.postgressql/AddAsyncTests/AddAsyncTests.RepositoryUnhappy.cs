namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.AddAsyncTests;

public partial class AddAsyncTests
{
    public static TheoryData<string, bool> NullEntityCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "AddAsync rejects null entities")]
    [MemberData(nameof(NullEntityCases))]
    public async Task AddAsync_NullEntity_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentNullException>(async () => await repo.AddAsync(null!, default));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentNullException>(async () => await singleRepo.AddAsync(null!, default));
    }

    public static TheoryData<string, bool> CancellationCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "AddAsync save path respects cancellation token")]
    [MemberData(nameof(CancellationCases))]
    public async Task AddAsync_CanceledToken_ThrowsOnSave(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(productId: DataSeeder.productBookId, customerId: DataSeeder.customerJohnId);
            await repo.AddAsync(entity, cts.Token);
            await Should.ThrowAsync<OperationCanceledException>(async () => await uow.SaveChangesAsync(cts.Token));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct();
        await singleRepo.AddAsync(product, cts.Token);
        await Should.ThrowAsync<OperationCanceledException>(async () => await uow.SaveChangesAsync(cts.Token));
    }

    public static TheoryData<string, bool> DuplicateCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "AddAsync duplicate keys fail on SaveChanges")]
    [MemberData(nameof(DuplicateCases))]
    public async Task AddAsync_Duplicate_ThrowsDbUpdateException(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var duplicate = CreateValidReview(
                productId: DataSeeder.productLaptopId,
                customerId: DataSeeder.customerJaneId,
                rating: 1,
                comment: "Should fail");

            await repo.AddAsync(duplicate);
            await Should.ThrowAsync<DbUpdateException>(async () => await uow.SaveChangesAsync());
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var duplicateProduct = CreateValidProduct(
            id: DataSeeder.productLaptopId,
            sku: "LP-15",
            name: "Duplicate PK Product");

        await singleRepo.AddAsync(duplicateProduct);
        await Should.ThrowAsync<DbUpdateException>(async () => await uow.SaveChangesAsync());
    }

    public static TheoryData<string, bool> WithoutSaveCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "AddAsync does not persist without SaveChanges")]
    [MemberData(nameof(WithoutSaveCases))]
    public async Task AddAsync_WithoutSaveChanges_DoesNotPersist(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(
                productId: DataSeeder.productBookId,
                customerId: DataSeeder.customerJohnId);

            await repo.AddAsync(entity);

            using var verifyScope = Factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var exists = await verifyDb.Reviews.AsNoTracking()
                .AnyAsync(x => x.ProductId == entity.ProductId && x.CustomerId == entity.CustomerId);
            exists.ShouldBeFalse();
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct();
        await singleRepo.AddAsync(product);

        using var verifySingleScope = Factory.Services.CreateScope();
        var verifySingleDb = verifySingleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await verifySingleDb.Products.AsNoTracking().AnyAsync(x => x.Id == product.Id);
        persisted.ShouldBeFalse();
    }
}
