namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.AddRangeAsyncTests;

public partial class AddRangeAsyncTests
{
    public static TheoryData<string, bool> NullCollectionCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "AddRangeAsync rejects null collections")]
    [MemberData(nameof(NullCollectionCases))]
    public async Task AddRangeAsync_NullCollection_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentNullException>(async () => await repo.AddRangeAsync(null!, default));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentNullException>(async () => await singleRepo.AddRangeAsync(null!, default));
    }

    public static TheoryData<string, bool> EmptyCollectionCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "AddRangeAsync rejects empty collections")]
    [MemberData(nameof(EmptyCollectionCases))]
    public async Task AddRangeAsync_EmptyCollection_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await repo.AddRangeAsync([], default));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await singleRepo.AddRangeAsync([], default));
    }

    public static TheoryData<string, bool, bool> DuplicateCases => new()
    {
        { "single-key-primary-key", false, false },
        { "single-key-unique-sku", false, true },
        { "composite-key-primary-key", true, false }
    };

    [Theory(DisplayName = "AddRangeAsync duplicate keys fail on SaveChanges")]
    [MemberData(nameof(DuplicateCases))]
    public async Task AddRangeAsync_Duplicate_ThrowsDbUpdateException(string caseId, bool compositeKey, bool uniqueSkuOnly)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var duplicates = new List<Review>
            {
                CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJaneId, rating: 1),
                CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 2)
            };

            await repo.AddRangeAsync(duplicates);
            await Should.ThrowAsync<DbUpdateException>(async () => await uow.SaveChangesAsync());
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var duplicateEntities = uniqueSkuOnly
            ? new List<Product>
            {
                CreateValidProduct(id: Guid.NewGuid(), sku: "LP-15", name: "Duplicate Unique SKU Product"),
                CreateValidProduct()
            }
            : new List<Product>
            {
                CreateValidProduct(id: DataSeeder.productLaptopId, sku: $"DUP-{Guid.NewGuid():N}", name: "Duplicate Product"),
                CreateValidProduct()
            };

        await singleRepo.AddRangeAsync(duplicateEntities);
        await Should.ThrowAsync<DbUpdateException>(async () => await uow.SaveChangesAsync());
    }

    public static TheoryData<string, bool> WithoutSaveCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "AddRangeAsync does not persist without SaveChanges")]
    [MemberData(nameof(WithoutSaveCases))]
    public async Task AddRangeAsync_WithoutSaveChanges_DoesNotPersist(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entities = new List<Review>
            {
                CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId),
                CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId)
            };

            await repo.AddRangeAsync(entities);

            using var verifyScope = Factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var exists = await verifyDb.Reviews.AsNoTracking()
                .AnyAsync(x =>
                    (x.ProductId == DataSeeder.productBookId && x.CustomerId == DataSeeder.customerJohnId) ||
                    (x.ProductId == DataSeeder.productHeadphonesId && x.CustomerId == DataSeeder.customerJaneId));
            exists.ShouldBeFalse();
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var productA = CreateValidProduct();
        var productB = CreateValidProduct();
        await singleRepo.AddRangeAsync([productA, productB]);

        using var verifySingleScope = Factory.Services.CreateScope();
        var verifySingleDb = verifySingleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var existsAny = await verifySingleDb.Products.AsNoTracking().AnyAsync(x => x.Id == productA.Id || x.Id == productB.Id);
        existsAny.ShouldBeFalse();
    }

    public static TheoryData<string, bool> CancellationCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "AddRangeAsync save path respects cancellation token")]
    [MemberData(nameof(CancellationCases))]
    public async Task AddRangeAsync_CanceledToken_ThrowsOnSave(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await repo.AddRangeAsync(
            [
                CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId),
                CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId)
            ], cts.Token);
            await Should.ThrowAsync<OperationCanceledException>(async () => await uow.SaveChangesAsync(cts.Token));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await singleRepo.AddRangeAsync([CreateValidProduct(), CreateValidProduct()], cts.Token);
        await Should.ThrowAsync<OperationCanceledException>(async () => await uow.SaveChangesAsync(cts.Token));
    }
}
