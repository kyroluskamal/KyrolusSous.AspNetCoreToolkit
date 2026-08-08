namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryRemoveAsyncTests;

public partial class TryRemoveAsyncTests
{
    [Theory(DisplayName = "TryRemoveAsync by entity rejects null entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryRemoveAsync_ByEntity_NullEntity_Throws(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentNullException>(async () => await repo.TryRemoveAsync((Review)null!, default));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentNullException>(async () => await singleRepo.TryRemoveAsync((Product)null!, default));
    }

    [Theory(DisplayName = "TryRemoveAsync by key returns not found for missing entities")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryRemoveAsync_ByKey_NotFound_ReturnsNotFound(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var result = await repo.TryRemoveAsync([Guid.NewGuid(), Guid.NewGuid()]);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.NotFound);
            result.Exception.ShouldBeNull();
            result.Value.ShouldBeFalse();
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var singleResult = await singleRepo.TryRemoveAsync(Guid.NewGuid());
        singleResult.Status.ShouldBe(KyrolusRepositoryOperationStatus.NotFound);
        singleResult.Exception.ShouldBeNull();
        singleResult.Value.ShouldBeFalse();
    }

    [Theory(DisplayName = "TryRemoveAsync by entity returns failed when entity is missing")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryRemoveAsync_ByEntity_NotFound_ReturnsFailedWithKeyNotFound(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var missing = CreateValidReview(Guid.NewGuid(), Guid.NewGuid(), rating: 1, comment: "missing");
            var result = await repo.TryRemoveAsync(missing);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
            result.Exception.ShouldBeOfType<KeyNotFoundException>();
            result.Value.ShouldBeFalse();
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var missingProduct = CreateValidProduct(id: Guid.NewGuid(), name: "missing");
        var singleResult = await singleRepo.TryRemoveAsync(missingProduct);
        singleResult.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
        singleResult.Exception.ShouldBeOfType<KeyNotFoundException>();
        singleResult.Value.ShouldBeFalse();
    }

    [Theory(DisplayName = "TryRemoveAsync by key returns failed for canceled tokens")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryRemoveAsync_ByKey_CanceledToken_ReturnsFailed(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (compositeKey)
        {
            var entity = CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 2, comment: "cancel-key-composite");
            await SeedReviewAsync(entity);

            try
            {
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var result = await repo.TryRemoveAsync([entity.ProductId, entity.CustomerId], cts.Token);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
                result.Exception.ShouldBeOfType<OperationCanceledException>();
                result.Value.ShouldBeFalse();
                (await ReviewExistsAsync(entity.ProductId, entity.CustomerId)).ShouldBeTrue();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var product = CreateValidProduct(name: "cancel-key-single");
        await SeedProductAsync(product);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var result = await repo.TryRemoveAsync(product.Id, cts.Token);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
            result.Exception.ShouldBeOfType<OperationCanceledException>();
            result.Value.ShouldBeFalse();
            (await ProductExistsAsync(product.Id)).ShouldBeTrue();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "TryRemoveAsync by entity returns failed for canceled tokens")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryRemoveAsync_ByEntity_CanceledToken_ReturnsFailed(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (compositeKey)
        {
            var entity = CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 3, comment: "cancel-entity-composite");
            await SeedReviewAsync(entity);

            try
            {
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var result = await repo.TryRemoveAsync(entity, cts.Token);
                result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
                result.Exception.ShouldBeOfType<OperationCanceledException>();
                result.Value.ShouldBeFalse();
                (await ReviewExistsAsync(entity.ProductId, entity.CustomerId)).ShouldBeTrue();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var product = CreateValidProduct(name: "cancel-entity-single");
        await SeedProductAsync(product);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var result = await repo.TryRemoveAsync(product, cts.Token);
            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
            result.Exception.ShouldBeOfType<OperationCanceledException>();
            result.Value.ShouldBeFalse();
            (await ProductExistsAsync(product.Id)).ShouldBeTrue();
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "TryRemoveAsync by key returns not found after entity is already removed")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task TryRemoveAsync_ByKey_SecondCall_ReturnsNotFound(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 4, comment: "second-call-composite");
            await SeedReviewAsync(entity);

            try
            {
                (await repo.TryRemoveAsync([entity.ProductId, entity.CustomerId])).Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);

                var second = await repo.TryRemoveAsync([entity.ProductId, entity.CustomerId]);
                second.Status.ShouldBe(KyrolusRepositoryOperationStatus.NotFound);
                second.Exception.ShouldBeNull();
                second.Value.ShouldBeFalse();
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "second-call-single");
        await SeedProductAsync(product);

        try
        {
            (await singleRepo.TryRemoveAsync(product.Id)).Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);

            var second = await singleRepo.TryRemoveAsync(product.Id);
            second.Status.ShouldBe(KyrolusRepositoryOperationStatus.NotFound);
            second.Exception.ShouldBeNull();
            second.Value.ShouldBeFalse();
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

    [Theory(DisplayName = "TryRemoveAsync composite key overload rejects invalid key arrays")]
    [MemberData(nameof(CompositeInvalidKeyCases))]
    public async Task TryRemoveAsync_Composite_InvalidKeys_Throws(string caseId, object?[]? keys)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        await Should.ThrowAsync<ArgumentException>(async () => await repo.TryRemoveAsync(keys));
    }
}
