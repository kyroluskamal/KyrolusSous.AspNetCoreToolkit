namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.PatchAsyncTests;

public partial class PatchAsyncTests
{
    public static TheoryData<string, bool> KeyTypeCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "PatchAsync rejects null updates")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task PatchAsync_NullUpdates_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentException>(async () =>
                await repo.PatchAsync([DataSeeder.productLaptopId, DataSeeder.customerJaneId], null!, default));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await singleRepo.PatchAsync(DataSeeder.productLaptopId, null!, default));
    }

    [Theory(DisplayName = "PatchAsync throws when entity is not found")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task PatchAsync_NotFound_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<KeyNotFoundException>(async () =>
                await repo.PatchAsync([Guid.NewGuid(), Guid.NewGuid()], new Dictionary<string, object> { ["Comment"] = "x" }));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<KeyNotFoundException>(async () =>
            await singleRepo.PatchAsync(Guid.NewGuid(), new Dictionary<string, object> { ["Name"] = "x" }));
    }

    [Theory(DisplayName = "PatchAsync throws for invalid property names")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task PatchAsync_InvalidProperty_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (compositeKey)
        {
            var seed = CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 3, comment: "Before");
            await SeedReviewAsync(seed);

            try
            {
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                await Should.ThrowAsync<InvalidOperationException>(async () =>
                    await repo.PatchAsync([seed.ProductId, seed.CustomerId], new Dictionary<string, object> { ["NoSuchProperty"] = "x" }));
            }
            finally
            {
                await CleanupReviewAsync(seed.ProductId, seed.CustomerId);
            }

            return;
        }

        var product = CreateValidProduct(name: "Before");
        await SeedProductAsync(product);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await repo.PatchAsync(product.Id, new Dictionary<string, object> { ["NoSuchProperty"] = "x" }));
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "PatchAsync does not persist without SaveChanges")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task PatchAsync_WithoutSaveChanges_DoesNotPersist(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (compositeKey)
        {
            var seed = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 3, comment: "Before");
            await SeedReviewAsync(seed);

            try
            {
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                await repo.PatchAsync([seed.ProductId, seed.CustomerId], new Dictionary<string, object> { ["Comment"] = "After" });

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

        var seedProduct = CreateValidProduct(name: "Before");
        await SeedProductAsync(seedProduct);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            await repo.PatchAsync(seedProduct.Id, new Dictionary<string, object> { ["Name"] = "After" });

            using var verifyScope = Factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await verifyDb.Products.AsNoTracking().SingleAsync(x => x.Id == seedProduct.Id);
            persisted.Name.ShouldBe("Before");
        }
        finally
        {
            await CleanupProductAsync(seedProduct.Id);
        }
    }

    [Theory(DisplayName = "PatchAsync respects cancellation token")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task PatchAsync_CanceledToken_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (compositeKey)
        {
            var seed = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "Before");
            await SeedReviewAsync(seed);

            try
            {
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                await Should.ThrowAsync<OperationCanceledException>(async () =>
                    await repo.PatchAsync([seed.ProductId, seed.CustomerId], new Dictionary<string, object> { ["Comment"] = "After" }, cts.Token));
            }
            finally
            {
                await CleanupReviewAsync(seed.ProductId, seed.CustomerId);
            }

            return;
        }

        var product = CreateValidProduct(name: "Before");
        await SeedProductAsync(product);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await repo.PatchAsync(product.Id, new Dictionary<string, object> { ["Name"] = "After" }, cts.Token));
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

    [Theory(DisplayName = "PatchAsync composite rejects invalid key arrays")]
    [MemberData(nameof(CompositeInvalidKeyCases))]
    public async Task PatchAsync_Composite_InvalidKeyValues_Throws(string caseId, object?[]? keyValues)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        await Should.ThrowAsync<ArgumentException>(async () =>
            await repo.PatchAsync(keyValues, new Dictionary<string, object> { ["Comment"] = "x" }));
    }

    [Fact(DisplayName = "PatchAsync composite rejects empty updates")]
    public async Task PatchAsync_Composite_EmptyUpdates_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        await Should.ThrowAsync<ArgumentException>(async () =>
            await repo.PatchAsync([DataSeeder.productLaptopId, DataSeeder.customerJaneId], []));
    }

    [Fact(DisplayName = "PatchAsync single-key save failure throws DbUpdateException")]
    public async Task PatchAsync_SingleKey_SaveFailure_ThrowsDbUpdateException()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        await repo.PatchAsync(DataSeeder.productLaptopId, new Dictionary<string, object>
        {
            ["StoreId"] = Guid.NewGuid()
        });

        await Should.ThrowAsync<DbUpdateException>(async () => await uow.SaveChangesAsync());
    }
}
