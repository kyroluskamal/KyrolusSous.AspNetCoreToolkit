namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryPatchAsyncTests;

public partial class TryPatchAsyncTests
{
    public static TheoryData<string, bool> KeyTypeCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "TryPatchAsync rejects null updates")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryPatchAsync_NullUpdates_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentException>(async () =>
                await repo.TryPatchAsync([DataSeeder.productLaptopId, DataSeeder.customerJaneId], null!));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentException>(async () =>
            await singleRepo.TryPatchAsync(DataSeeder.productLaptopId, null!));
    }

    [Theory(DisplayName = "TryPatchAsync rejects empty updates")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryPatchAsync_EmptyUpdates_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentException>(async () =>
                await repo.TryPatchAsync([DataSeeder.productLaptopId, DataSeeder.customerJaneId], []));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentException>(async () =>
            await singleRepo.TryPatchAsync(DataSeeder.productLaptopId, []));
    }

    [Theory(DisplayName = "TryPatchAsync returns failed when entity is not found")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryPatchAsync_NotFound_ReturnsFailedWithKeyNotFound(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var result = await repo.TryPatchAsync([Guid.NewGuid(), Guid.NewGuid()], new Dictionary<string, object> { ["Comment"] = "x" });
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
            result.Exception.ShouldBeOfType<KeyNotFoundException>();
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleResult = await singleRepo.TryPatchAsync(Guid.NewGuid(), new Dictionary<string, object> { ["Name"] = "x" });
        singleResult.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
        singleResult.Exception.ShouldBeOfType<KeyNotFoundException>();
    }

    [Theory(DisplayName = "TryPatchAsync returns failed for invalid property names")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryPatchAsync_InvalidProperty_ReturnsFailed(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (compositeKey)
        {
            var seed = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 4, comment: "Before");
            await SeedReviewAsync(seed);

            try
            {
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var result = await repo.TryPatchAsync([seed.ProductId, seed.CustomerId], new Dictionary<string, object> { ["NoSuchProperty"] = "x" });
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
                result.Exception.ShouldBeOfType<InvalidOperationException>();
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
            var result = await repo.TryPatchAsync(product.Id, new Dictionary<string, object> { ["NoSuchProperty"] = "x" });
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
            result.Exception.ShouldBeOfType<InvalidOperationException>();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "TryPatchAsync does not persist without SaveChanges")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryPatchAsync_WithoutSaveChanges_DoesNotPersist(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (compositeKey)
        {
            var seed = CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 2, comment: "Before");
            await SeedReviewAsync(seed);

            try
            {
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var result = await repo.TryPatchAsync([seed.ProductId, seed.CustomerId], new Dictionary<string, object> { ["Comment"] = "After" });
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);

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
            var result = await repo.TryPatchAsync(seedProduct.Id, new Dictionary<string, object> { ["Name"] = "After" });
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);

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

    [Theory(DisplayName = "TryPatchAsync respects cancellation token")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryPatchAsync_CanceledToken_ReturnsFailed(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (compositeKey)
        {
            var seed = CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 2, comment: "Before");
            await SeedReviewAsync(seed);

            try
            {
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var result = await repo.TryPatchAsync([seed.ProductId, seed.CustomerId], new Dictionary<string, object> { ["Comment"] = "After" }, cts.Token);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
                result.Exception.ShouldBeOfType<OperationCanceledException>();
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
            var result = await repo.TryPatchAsync(product.Id, new Dictionary<string, object> { ["Name"] = "After" }, cts.Token);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
            result.Exception.ShouldBeOfType<OperationCanceledException>();
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
        { "missing-key", [DataSeeder.productLaptopId] },
        { "extra-key", [DataSeeder.productLaptopId, DataSeeder.customerJaneId, Guid.NewGuid()] }
    };

    [Theory(DisplayName = "TryPatchAsync composite rejects invalid key arrays")]
    [MemberData(nameof(CompositeInvalidKeyCases))]
    public async Task TryPatchAsync_Composite_InvalidKeys_Throws(string caseId, object?[]? keys)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        await Should.ThrowAsync<ArgumentException>(async () =>
            await repo.TryPatchAsync(keys, new Dictionary<string, object> { ["Comment"] = "x" }));
    }

    [Fact(DisplayName = "TryPatchAsync save failure surfaces on SaveChanges")]
    public async Task TryPatchAsync_SingleKey_SaveFailure_ThrowsOnSaveChanges()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        var result = await repo.TryPatchAsync(DataSeeder.productLaptopId, new Dictionary<string, object>
        {
            ["StoreId"] = Guid.NewGuid()
        });

        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
        result.Exception.ShouldBeNull();
        await Should.ThrowAsync<DbUpdateException>(async () => await uow.SaveChangesAsync());
    }
}
