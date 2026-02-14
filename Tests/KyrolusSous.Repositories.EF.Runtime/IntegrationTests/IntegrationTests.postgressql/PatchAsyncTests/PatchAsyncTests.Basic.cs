namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.PatchAsyncTests;

public partial class PatchAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private sealed record PatchSingleKeySpec(
        Func<Product> SeedEntity,
        Func<Dictionary<string, object>> BuildUpdates,
        Action<Product, Product> AssertPersisted,
        bool ExpectChanges = true);

    private sealed record PatchCompositeKeySpec(
        Func<Review> SeedEntity,
        Func<Dictionary<string, object>> BuildUpdates,
        Action<Review, Review> AssertPersisted);

    private static readonly IReadOnlyDictionary<string, PatchSingleKeySpec> SingleKeySuccessSpecs = BuildSingleKeySuccessSpecs();
    private static readonly IReadOnlyDictionary<string, PatchCompositeKeySpec> CompositeKeySuccessSpecs = BuildCompositeKeySuccessSpecs();

    public static TheoryData<string> SingleKeySuccessCases => CaseIdsFrom(SingleKeySuccessSpecs);
    public static TheoryData<string> CompositeKeySuccessCases => CaseIdsFrom(CompositeKeySuccessSpecs);

    [Theory(DisplayName = "PatchAsync updates single-key entities")]
    [MemberData(nameof(SingleKeySuccessCases))]
    public async Task PatchAsync_SingleKey_UpdatesEntity(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SingleKeySuccessSpecs[caseId];
        var seed = spec.SeedEntity();
        await SeedProductAsync(seed);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
            var patched = await repo.PatchAsync(seed.Id, spec.BuildUpdates());
            patched.ShouldNotBeNull();

            var affected = await uow.SaveChangesAsync();
            if (spec.ExpectChanges)
                affected.ShouldBeGreaterThan(0);
            else
                affected.ShouldBe(0);

            using var verifyScope = Factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await verifyDb.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == seed.Id);
            persisted.ShouldNotBeNull();
            spec.AssertPersisted(seed, persisted!);
        }
        finally
        {
            await CleanupProductAsync(seed.Id);
        }
    }

    [Theory(DisplayName = "PatchAsync updates composite-key entities")]
    [MemberData(nameof(CompositeKeySuccessCases))]
    public async Task PatchAsync_CompositeKey_UpdatesEntity(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = CompositeKeySuccessSpecs[caseId];
        var seed = spec.SeedEntity();
        await SeedReviewAsync(seed);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
            var patched = await repo.PatchAsync([seed.ProductId, seed.CustomerId], spec.BuildUpdates());
            patched.ShouldNotBeNull();
            var affected = await uow.SaveChangesAsync();
            affected.ShouldBeGreaterThan(0);

            using var verifyScope = Factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await verifyDb.Reviews.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ProductId == seed.ProductId && x.CustomerId == seed.CustomerId);
            persisted.ShouldNotBeNull();
            spec.AssertPersisted(seed, persisted!);
        }
        finally
        {
            await CleanupReviewAsync(seed.ProductId, seed.CustomerId);
        }
    }

    [Fact(DisplayName = "Patch API single-key accepts empty update object as no-op")]
    public async Task PatchAsync_Api_SingleKey_EmptyUpdates_NoOp()
    {
        var seed = CreateValidProduct(name: "Patch-Api-Before");
        await SeedProductAsync(seed);

        try
        {
            var (response, content) = await PatchSingleKeyAsync<Product>(seed.Id, new Dictionary<string, object?>());
            response.StatusCode.ShouldBe(HttpStatusCode.OK, content);
            var responseEntity = JsonSerializer.Deserialize<Product>(content, JsonOptions);
            responseEntity.ShouldNotBeNull();
            responseEntity!.Id.ShouldBe(seed.Id);

            using var verifyScope = Factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await verifyDb.Products.AsNoTracking().SingleAsync(x => x.Id == seed.Id);
            persisted.Name.ShouldBe("Patch-Api-Before");
        }
        finally
        {
            await CleanupProductAsync(seed.Id);
        }
    }

    private static IReadOnlyDictionary<string, PatchSingleKeySpec> BuildSingleKeySuccessSpecs()
        => new Dictionary<string, PatchSingleKeySpec>
        {
            ["scalar-fields"] = new(
                SeedEntity: () => CreateValidProduct(name: "Before-Scalar", price: 25m, stockQuantity: 6),
                BuildUpdates: () => new Dictionary<string, object>
                {
                    ["Name"] = "After-Scalar",
                    ["Price"] = 99m,
                    ["StockQuantity"] = 42
                },
                AssertPersisted: (_, persisted) =>
                {
                    persisted.Name.ShouldBe("After-Scalar");
                    persisted.Price.ShouldBe(99m);
                    persisted.StockQuantity.ShouldBe(42);
                }),

            ["nullable-fields"] = new(
                SeedEntity: () => CreateValidProduct(weight: 1.25m, count: 5, addedAt: new TimeOnly(9, 30)),
                BuildUpdates: () => new Dictionary<string, object>
                {
                    ["Weight"] = null!,
                    ["Count"] = null!,
                    ["AddedAt"] = null!
                },
                AssertPersisted: (_, persisted) =>
                {
                    persisted.Weight.ShouldBeNull();
                    persisted.Count.ShouldBeNull();
                    persisted.AddedAt.ShouldBeNull();
                }),

            ["empty-updates-noop"] = new(
                SeedEntity: () => CreateValidProduct(name: "Before-NoOp", price: 64.5m, stockQuantity: 12),
                BuildUpdates: () => [],
                AssertPersisted: (seed, persisted) =>
                {
                    persisted.Name.ShouldBe(seed.Name);
                    persisted.Price.ShouldBe(seed.Price);
                    persisted.StockQuantity.ShouldBe(seed.StockQuantity);
                },
                ExpectChanges: false)
        };

    private static IReadOnlyDictionary<string, PatchCompositeKeySpec> BuildCompositeKeySuccessSpecs()
        => new Dictionary<string, PatchCompositeKeySpec>
        {
            ["scalar-fields"] = new(
                SeedEntity: () => CreateValidReview(
                    productId: DataSeeder.productBookId,
                    customerId: DataSeeder.customerJohnId,
                    rating: 2,
                    comment: "Before-Composite"),
                BuildUpdates: () => new Dictionary<string, object>
                {
                    ["Rating"] = 5,
                    ["Comment"] = "After-Composite",
                    ["FinishedAt"] = TimeSpan.FromHours(12)
                },
                AssertPersisted: (_, persisted) =>
                {
                    persisted.Rating.ShouldBe(5);
                    persisted.Comment.ShouldBe("After-Composite");
                    persisted.FinishedAt.ShouldBe(TimeSpan.FromHours(12));
                }),

            ["nullable-fields"] = new(
                SeedEntity: () => CreateValidReview(
                    productId: DataSeeder.productHeadphonesId,
                    customerId: DataSeeder.customerJaneId,
                    rating: 3,
                    comment: "HasComment",
                    addedAt: new TimeOnly(14, 0)),
                BuildUpdates: () => new Dictionary<string, object>
                {
                    ["Comment"] = null!,
                    ["AddedAt"] = null!
                },
                AssertPersisted: (_, persisted) =>
                {
                    persisted.Comment.ShouldBeNull();
                    persisted.AddedAt.ShouldBeNull();
                })
        };
}
