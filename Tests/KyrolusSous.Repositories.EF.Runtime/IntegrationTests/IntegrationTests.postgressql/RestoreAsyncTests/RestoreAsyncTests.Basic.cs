namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.RestoreAsyncTests;

public partial class RestoreAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    [Theory(DisplayName = "RestoreAsync restores soft-deleted entities after SaveChanges")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task RestoreAsync_AfterSave_RestoresEntity(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 3, comment: "restore-composite");
            await SeedReviewAsync(entity);

            try
            {
                await SoftDeleteReviewAsync(entity.ProductId, entity.CustomerId);
                (await IsReviewDeletedAsync(entity.ProductId, entity.CustomerId)).ShouldBe(true);

                var restored = await repo.RestoreAsync([entity.ProductId, entity.CustomerId]);
                restored.ShouldBeTrue();

                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
                (await IsReviewDeletedAsync(entity.ProductId, entity.CustomerId)).ShouldBe(false);
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "restore-single");
        await SeedProductAsync(product);

        try
        {
            await SoftDeleteProductAsync(product.Id);
            (await IsProductDeletedAsync(product.Id)).ShouldBe(true);

            var restored = await singleRepo.RestoreAsync(product.Id);
            restored.ShouldBeTrue();

            (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
            (await IsProductDeletedAsync(product.Id)).ShouldBe(false);
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    [Theory(DisplayName = "RestoreAsync does not persist without SaveChanges")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task RestoreAsync_WithoutSaveChanges_DoesNotPersist(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var entity = CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 2, comment: "restore-without-save");
            await SeedReviewAsync(entity);

            try
            {
                await SoftDeleteReviewAsync(entity.ProductId, entity.CustomerId);
                var restored = await repo.RestoreAsync([entity.ProductId, entity.CustomerId]);
                restored.ShouldBeTrue();

                (await IsReviewDeletedAsync(entity.ProductId, entity.CustomerId)).ShouldBe(true);
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var product = CreateValidProduct(name: "restore-without-save-single");
        await SeedProductAsync(product);

        try
        {
            await SoftDeleteProductAsync(product.Id);
            var restored = await singleRepo.RestoreAsync(product.Id);
            restored.ShouldBeTrue();

            (await IsProductDeletedAsync(product.Id)).ShouldBe(true);
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    private sealed record ApiSuccessSpec(bool IsComposite, Func<(Guid ProductId, Guid CustomerId)>? CompositeSeed, Func<Guid>? SingleSeed);

    private static readonly IReadOnlyDictionary<string, ApiSuccessSpec> ApiSuccessSpecs = BuildApiSuccessSpecs();
    public static TheoryData<string> ApiSuccessCases => CaseIdsFrom(ApiSuccessSpecs);

    [Theory(DisplayName = "RestoreAsync API restores soft-deleted entities")]
    [MemberData(nameof(ApiSuccessCases))]
    public async Task RestoreAsync_Api_Success(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = ApiSuccessSpecs[caseId];

        if (spec.IsComposite)
        {
            var key = spec.CompositeSeed!.Invoke();
            var entity = CreateValidReview(key.ProductId, key.CustomerId, rating: 4, comment: $"restore-api-composite-{Guid.NewGuid():N}");
            await SeedReviewAsync(entity);

            try
            {
                await SoftDeleteReviewAsync(entity.ProductId, entity.CustomerId);
                (await IsReviewDeletedAsync(entity.ProductId, entity.CustomerId)).ShouldBe(true);

                var (response, content) = await PostCompositeRestoreAsync<Review>([entity.ProductId, entity.CustomerId]);
                response.StatusCode.ShouldBe(HttpStatusCode.NoContent, content);
                content.ShouldBeEmpty();
                (await IsReviewDeletedAsync(entity.ProductId, entity.CustomerId)).ShouldBe(false);
            }
            finally
            {
                await CleanupReviewAsync(entity.ProductId, entity.CustomerId);
            }

            return;
        }

        var id = spec.SingleSeed!.Invoke();
        var product = CreateValidProduct(id: id, name: $"restore-api-single-{Guid.NewGuid():N}");
        await SeedProductAsync(product);

        try
        {
            await SoftDeleteProductAsync(product.Id);
            (await IsProductDeletedAsync(product.Id)).ShouldBe(true);

            var (response, content) = await PostSingleRestoreAsync<Product>(product.Id);
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent, content);
            content.ShouldBeEmpty();
            (await IsProductDeletedAsync(product.Id)).ShouldBe(false);
        }
        finally
        {
            await CleanupProductAsync(product.Id);
        }
    }

    private static IReadOnlyDictionary<string, ApiSuccessSpec> BuildApiSuccessSpecs()
        => new Dictionary<string, ApiSuccessSpec>
        {
            ["single"] = new(
                IsComposite: false,
                CompositeSeed: null,
                SingleSeed: () => Guid.NewGuid()),
            ["composite"] = new(
                IsComposite: true,
                CompositeSeed: () => (DataSeeder.productLaptopId, DataSeeder.customerJohnId),
                SingleSeed: null)
        };
}
