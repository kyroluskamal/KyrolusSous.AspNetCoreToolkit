namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetDeletedOnlyAsyncTests;

public partial class GetDeletedOnlyAsyncTests
{
    [Theory(DisplayName = "GetDeletedOnlyAsync returns empty when no deleted rows match")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetDeletedOnlyAsync_NoDeletedMatch_ReturnsEmpty(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];

        if (compositeKey)
        {
            var prefix = $"no-deleted-c-{Guid.NewGuid():N}";
            var entity = CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 4, comment: prefix);
            await SeedReviewAsync(entity);

            try
            {
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var items = await repo.GetDeletedOnlyAsync(x => x.Comment == prefix);
                items.ShouldBeEmpty();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singlePrefix = $"no-deleted-s-{Guid.NewGuid():N}";
        var product = CreateValidProduct(name: singlePrefix, sku: singlePrefix);
        await SeedProductAsync(product);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var items = await repo.GetDeletedOnlyAsync(x => x.Sku == singlePrefix);
            items.ShouldBeEmpty();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "GetDeletedOnlyAsync does not include unsaved soft-delete changes")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetDeletedOnlyAsync_WithoutSaveChanges_DoesNotIncludeRows(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];

        if (compositeKey)
        {
            var prefix = $"nosave-c-{Guid.NewGuid():N}";
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 3, comment: prefix);
            await SeedReviewAsync(entity);

            try
            {
                using (var scope = Factory.Services.CreateScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                    (await repo.SoftDeleteAsync([entity.ProductId, entity.CustomerId])).ShouldBeTrue();
                }

                using var verifyScope = Factory.Services.CreateScope();
                var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var items = await verifyRepo.GetDeletedOnlyAsync(x => x.Comment == prefix);
                items.ShouldBeEmpty();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singlePrefix = $"nosave-s-{Guid.NewGuid():N}";
        var product = CreateValidProduct(name: singlePrefix, sku: singlePrefix);
        await SeedProductAsync(product);

        try
        {
            using (var scope = Factory.Services.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
                (await repo.SoftDeleteAsync(product.Id)).ShouldBeTrue();
            }

            using var verifyScope = Factory.Services.CreateScope();
            var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var items = await verifyRepo.GetDeletedOnlyAsync(x => x.Sku == singlePrefix);
            items.ShouldBeEmpty();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "GetDeletedOnlyAsync respects cancellation token")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetDeletedOnlyAsync_CanceledToken_Throws(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (compositeKey)
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await repo.GetDeletedOnlyAsync(x => x.Rating > 0, cancellationToken: cts.Token));
            return;
        }

        using var singleScope = Factory.Services.CreateScope();
        var singleRepo = singleScope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await singleRepo.GetDeletedOnlyAsync(x => x.Price > 0, cancellationToken: cts.Token));
    }

    [Theory(DisplayName = "GetDeletedOnlyAsync rejects invalid include paths")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetDeletedOnlyAsync_InvalidInclude_Throws(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];

        if (compositeKey)
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<InvalidOperationException>(async () =>
                await repo.GetDeletedOnlyAsync(includeProperties: ["NoSuchProperty"]));
            return;
        }

        using var singleScope = Factory.Services.CreateScope();
        var singleRepo = singleScope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await singleRepo.GetDeletedOnlyAsync(includeProperties: ["NoSuchProperty"]));
    }

    private sealed record GlobalFilterSpec(
        bool IsComposite,
        KyrolusRepositoryPolicy Policy,
        bool ExpectedAnyResult);

    private static readonly IReadOnlyDictionary<string, GlobalFilterSpec> GlobalFilterSpecs = BuildGlobalFilterSpecs();
    public static TheoryData<string> GlobalFilterCases => CaseIdsFrom(GlobalFilterSpecs);

    [Theory(DisplayName = "GetDeletedOnlyAsync respects global filters")]
    [MemberData(nameof(GlobalFilterCases))]
    public async Task GetDeletedOnlyAsync_GlobalFilter_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = GlobalFilterSpecs[caseId];
        var customFactory = WithPolicy(spec.Policy);

        if (spec.IsComposite)
        {
            var key = (ProductId: DataSeeder.productHeadphonesId, CustomerId: DataSeeder.customerJaneId);
            var tag = $"gf-c-{Guid.NewGuid():N}";

            try
            {
                await using (var scope = customFactory.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    db.Reviews.Add(CreateValidReview(key.ProductId, key.CustomerId, rating: 2, comment: tag));
                    await db.SaveChangesAsync();

                    if (spec.ExpectedAnyResult)
                    {
                        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
                        (await repo.SoftDeleteAsync([key.ProductId, key.CustomerId])).ShouldBeTrue();
                        (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
                    }
                    else
                    {
                        var entity = await db.Reviews.IgnoreQueryFilters()
                            .SingleAsync(x => x.ProductId == key.ProductId && x.CustomerId == key.CustomerId);
                        entity.IsDeleted = true;
                        entity.DeletedAt = DateTimeOffset.UtcNow;
                        await db.SaveChangesAsync();
                    }
                }

                await using (var scope = customFactory.Services.CreateAsyncScope())
                {
                    var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                    var items = await repo.GetDeletedOnlyAsync(x => x.Comment == tag);
                    items.Any().ShouldBe(spec.ExpectedAnyResult);
                }
            }
            finally
            {
                await CleanupReviewAsync(key.ProductId, key.CustomerId);
            }

            return;
        }

        var id = Guid.NewGuid();
        var singleTag = $"gf-s-{Guid.NewGuid():N}";
        try
        {
            await using (var scope = customFactory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Products.Add(CreateValidProduct(id: id, sku: singleTag, name: singleTag, price: 10m));
                await db.SaveChangesAsync();

                if (spec.ExpectedAnyResult)
                {
                    var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
                    var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
                    (await repo.SoftDeleteAsync(id)).ShouldBeTrue();
                    (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
                }
                else
                {
                    var entity = await db.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == id);
                    entity.IsDeleted = true;
                    entity.DeletedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync();
                }
            }

            await using (var scope = customFactory.Services.CreateAsyncScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
                var items = await repo.GetDeletedOnlyAsync(x => x.Sku == singleTag);
                items.Any().ShouldBe(spec.ExpectedAnyResult);
            }
        }
        finally
        {
            await CleanupProductAsync(id);
        }
    }

    private static IReadOnlyDictionary<string, GlobalFilterSpec> BuildGlobalFilterSpecs()
        => new Dictionary<string, GlobalFilterSpec>
        {
            ["single-blocked"] = new(
                IsComposite: false,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(x => x.Price > 1000m),
                ExpectedAnyResult: false),
            ["single-allowed"] = new(
                IsComposite: false,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Product>(x => x.Price >= 10m),
                ExpectedAnyResult: true),
            ["composite-blocked"] = new(
                IsComposite: true,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Review>(x => x.Rating >= 4),
                ExpectedAnyResult: false),
            ["composite-allowed"] = new(
                IsComposite: true,
                Policy: new KyrolusRepositoryPolicy().AddGlobalWhereFilter<Review>(x => x.Rating <= 2),
                ExpectedAnyResult: true)
        };
}
