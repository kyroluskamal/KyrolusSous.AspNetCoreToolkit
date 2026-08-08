namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.UpdateRangeAsyncTests;

public partial class UpdateRangeAsyncTests
{
    public static TheoryData<string, bool> NullCollectionCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "UpdateRangeAsync rejects null collections")]
    [MemberData(nameof(NullCollectionCases))]
    public async Task UpdateRangeAsync_NullCollection_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentNullException>(async () => await repo.UpdateRangeAsync(null!, default));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentNullException>(async () => await singleRepo.UpdateRangeAsync(null!, default));
    }

    public static TheoryData<string, bool> EmptyCollectionCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "UpdateRangeAsync rejects empty collections")]
    [MemberData(nameof(EmptyCollectionCases))]
    public async Task UpdateRangeAsync_EmptyCollection_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await repo.UpdateRangeAsync([], default));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () => await singleRepo.UpdateRangeAsync([], default));
    }

    public static TheoryData<string, bool> NotFoundCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "UpdateRangeAsync throws when any entity is not found")]
    [MemberData(nameof(NotFoundCases))]
    public async Task UpdateRangeAsync_NotFound_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var missing = CreateValidReview(Guid.NewGuid(), DataSeeder.customerJaneId, rating: 2);
            await Should.ThrowAsync<KeyNotFoundException>(async () => await repo.UpdateRangeAsync([missing]));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var missingProduct = CreateValidProduct(id: Guid.NewGuid(), name: "missing");
        await Should.ThrowAsync<KeyNotFoundException>(async () => await singleRepo.UpdateRangeAsync([missingProduct]));
    }

    public static TheoryData<string, bool> MixedFoundAndMissingCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "UpdateRangeAsync throws when collection mixes existing and missing entities")]
    [MemberData(nameof(MixedFoundAndMissingCases))]
    public async Task UpdateRangeAsync_MixedFoundAndMissing_ThrowsAndDoesNotPersist(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (compositeKey)
        {
            var seed = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "Before");
            await SeedReviewsAsync([seed]);

            try
            {
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var existingUpdate = Clone(seed);
                existingUpdate.Comment = "After";
                var updates = new List<Review>
                {
                    existingUpdate,
                    CreateValidReview(Guid.NewGuid(), DataSeeder.customerJohnId, rating: 5, comment: "Missing")
                };

                await Should.ThrowAsync<KeyNotFoundException>(async () => await repo.UpdateRangeAsync(updates));

                using var verifyScope = Factory.Services.CreateScope();
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var persisted = await verifyDb.Reviews.AsNoTracking()
                    .SingleAsync(x => x.ProductId == seed.ProductId && x.CustomerId == seed.CustomerId);
                persisted.Comment.ShouldBe("Before");
            }
            finally
            {
                await CleanupReviewsAsync([(seed.ProductId, seed.CustomerId)]);
            }

            return;
        }

        var singleSeed = CreateValidProduct(name: "Before");
        await SeedProductsAsync([singleSeed]);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var existingUpdate = Clone(singleSeed);
            existingUpdate.Name = "After";
            var updates = new List<Product>
            {
                existingUpdate,
                CreateValidProduct(id: Guid.NewGuid(), name: "Missing")
            };

            await Should.ThrowAsync<KeyNotFoundException>(async () => await repo.UpdateRangeAsync(updates));

            using var verifyScope = Factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await verifyDb.Products.AsNoTracking().SingleAsync(x => x.Id == singleSeed.Id);
            persisted.Name.ShouldBe("Before");
        }
        finally
        {
            await CleanupProductsAsync([singleSeed.Id]);
        }
    }

    public static TheoryData<string, bool> NullEntityCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "UpdateRangeAsync throws when collection contains null entity")]
    [MemberData(nameof(NullEntityCases))]
    public async Task UpdateRangeAsync_ContainsNullEntity_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var valid = CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJaneId, rating: 5);
            await Should.ThrowAsync<ArgumentNullException>(async () => await repo.UpdateRangeAsync([valid, null!]));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var validProduct = CreateValidProduct(id: DataSeeder.productLaptopId, name: "valid");
        await Should.ThrowAsync<ArgumentNullException>(async () => await singleRepo.UpdateRangeAsync([validProduct, null!]));
    }

    public static TheoryData<string> SingleKeySaveFailureCases => new()
    {
        "duplicate-sku",
        "invalid-store"
    };

    [Theory(DisplayName = "UpdateRangeAsync single-key save failures throw DbUpdateException")]
    [MemberData(nameof(SingleKeySaveFailureCases))]
    public async Task UpdateRangeAsync_SaveFailure_ThrowsDbUpdateException(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        using var readScope = Factory.Services.CreateScope();
        var db = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var laptop = await db.Products.AsNoTracking().SingleAsync(x => x.Id == DataSeeder.productLaptopId);
        var headphones = await db.Products.AsNoTracking().SingleAsync(x => x.Id == DataSeeder.productHeadphonesId);

        var updateA = Clone(laptop);
        var updateB = Clone(headphones);

        if (caseId == "duplicate-sku")
        {
            updateA.Sku = updateB.Sku;
        }
        else
        {
            updateA.StoreId = Guid.NewGuid();
        }

        await repo.UpdateRangeAsync([updateA, updateB]);
        await Should.ThrowAsync<DbUpdateException>(async () => await uow.SaveChangesAsync());
    }

    public static TheoryData<string, bool> WithoutSaveCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "UpdateRangeAsync does not persist without SaveChanges")]
    [MemberData(nameof(WithoutSaveCases))]
    public async Task UpdateRangeAsync_WithoutSaveChanges_DoesNotPersist(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        if (compositeKey)
        {
            var seed = new List<Review>
            {
                CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 2, comment: "Before-A"),
                CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 3, comment: "Before-B")
            };
            await SeedReviewsAsync(seed);

            try
            {
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var updates = seed.Select(Clone).ToList();
                updates[0].Comment = "After-A";
                updates[1].Comment = "After-B";

                await repo.UpdateRangeAsync(updates);

                using var verifyScope = Factory.Services.CreateScope();
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var persisted = await verifyDb.Reviews.AsNoTracking()
                    .Where(x =>
                        (x.ProductId == seed[0].ProductId && x.CustomerId == seed[0].CustomerId) ||
                        (x.ProductId == seed[1].ProductId && x.CustomerId == seed[1].CustomerId))
                    .ToListAsync();

                persisted.Any(x => x.Comment == "After-A" || x.Comment == "After-B").ShouldBeFalse();
            }
            finally
            {
                await CleanupReviewsAsync(seed.Select(x => (x.ProductId, x.CustomerId)));
            }

            return;
        }

        var singleSeed = new List<Product>
        {
            CreateValidProduct(name: "Before-A"),
            CreateValidProduct(name: "Before-B")
        };
        await SeedProductsAsync(singleSeed);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var updates = singleSeed.Select(Clone).ToList();
            updates[0].Name = "After-A";
            updates[1].Name = "After-B";
            await repo.UpdateRangeAsync(updates);

            using var verifyScope = Factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ids = singleSeed.Select(x => x.Id).ToList();
            var persisted = await verifyDb.Products.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync();
            persisted.Any(x => x.Name == "After-A" || x.Name == "After-B").ShouldBeFalse();
        }
        finally
        {
            await CleanupProductsAsync(singleSeed.Select(x => x.Id));
        }
    }

    public static TheoryData<string, bool> CancellationCases => new()
    {
        { "single-key", false },
        { "composite-key", true }
    };

    [Theory(DisplayName = "UpdateRangeAsync respects cancellation token")]
    [MemberData(nameof(CancellationCases))]
    public async Task UpdateRangeAsync_CanceledToken_Throws(string caseId, bool compositeKey)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var updates = new List<Review>
            {
                CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJaneId, rating: 5),
                CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJaneId, rating: 4)
            };
            await Should.ThrowAsync<OperationCanceledException>(async () => await repo.UpdateRangeAsync(updates, cts.Token));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var productUpdates = new List<Product>
        {
            CreateValidProduct(id: DataSeeder.productLaptopId, name: "Canceled A"),
            CreateValidProduct(id: DataSeeder.productHeadphonesId, name: "Canceled B")
        };
        await Should.ThrowAsync<OperationCanceledException>(async () => await singleRepo.UpdateRangeAsync(productUpdates, cts.Token));
    }
}
