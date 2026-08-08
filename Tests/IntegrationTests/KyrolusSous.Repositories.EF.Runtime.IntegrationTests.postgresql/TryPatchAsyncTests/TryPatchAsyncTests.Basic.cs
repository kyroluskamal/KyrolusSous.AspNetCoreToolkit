namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryPatchAsyncTests;

public partial class TryPatchAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private sealed record TryPatchSingleKeySpec(
        Func<Product> SeedEntity,
        Func<Dictionary<string, object>> BuildUpdates,
        Action<Product> AssertPersisted);

    private sealed record TryPatchCompositeKeySpec(
        Func<Review> SeedEntity,
        Func<Dictionary<string, object>> BuildUpdates,
        Action<Review> AssertPersisted);

    private static readonly IReadOnlyDictionary<string, TryPatchSingleKeySpec> SingleKeySuccessSpecs = BuildSingleKeySuccessSpecs();
    private static readonly IReadOnlyDictionary<string, TryPatchCompositeKeySpec> CompositeKeySuccessSpecs = BuildCompositeKeySuccessSpecs();

    public static TheoryData<string> SingleKeySuccessCases => CaseIdsFrom(SingleKeySuccessSpecs);
    public static TheoryData<string> CompositeKeySuccessCases => CaseIdsFrom(CompositeKeySuccessSpecs);

    [Theory(DisplayName = "TryPatchAsync succeeds for single-key entities")]
    [MemberData(nameof(SingleKeySuccessCases))]
    public async Task TryPatchAsync_SingleKey_Success(string caseId)
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
            var result = await repo.TryPatchAsync(seed.Id, spec.BuildUpdates());

            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            result.Exception.ShouldBeNull();
            result.Value.ShouldNotBeNull();

            var affected = await uow.SaveChangesAsync();
            affected.ShouldBeGreaterThan(0);

            using var verifyScope = Factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await verifyDb.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == seed.Id);
            persisted.ShouldNotBeNull();
            spec.AssertPersisted(persisted!);
        }
        finally
        {
            await CleanupProductAsync(seed.Id);
        }
    }

    [Theory(DisplayName = "TryPatchAsync succeeds for composite-key entities")]
    [MemberData(nameof(CompositeKeySuccessCases))]
    public async Task TryPatchAsync_CompositeKey_Success(string caseId)
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
            var result = await repo.TryPatchAsync([seed.ProductId, seed.CustomerId], spec.BuildUpdates());

            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            result.Exception.ShouldBeNull();
            result.Value.ShouldNotBeNull();

            var affected = await uow.SaveChangesAsync();
            affected.ShouldBeGreaterThan(0);

            using var verifyScope = Factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await verifyDb.Reviews.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ProductId == seed.ProductId && x.CustomerId == seed.CustomerId);
            persisted.ShouldNotBeNull();
            spec.AssertPersisted(persisted!);
        }
        finally
        {
            await CleanupReviewAsync(seed.ProductId, seed.CustomerId);
        }
    }

    private static IReadOnlyDictionary<string, TryPatchSingleKeySpec> BuildSingleKeySuccessSpecs()
        => new Dictionary<string, TryPatchSingleKeySpec>
        {
            ["scalar-fields"] = new(
                SeedEntity: () => CreateValidProduct(name: "Before-Single", price: 50m, stockQuantity: 11),
                BuildUpdates: () => new Dictionary<string, object>
                {
                    ["Name"] = "After-Single",
                    ["Price"] = 95m,
                    ["StockQuantity"] = 77
                },
                AssertPersisted: entity =>
                {
                    entity.Name.ShouldBe("After-Single");
                    entity.Price.ShouldBe(95m);
                    entity.StockQuantity.ShouldBe(77);
                }),

            ["nullable-fields"] = new(
                SeedEntity: () => CreateValidProduct(weight: 1.2m, count: 8, addedAt: new TimeOnly(10, 15)),
                BuildUpdates: () => new Dictionary<string, object>
                {
                    ["Weight"] = null!,
                    ["Count"] = null!,
                    ["AddedAt"] = null!
                },
                AssertPersisted: entity =>
                {
                    entity.Weight.ShouldBeNull();
                    entity.Count.ShouldBeNull();
                    entity.AddedAt.ShouldBeNull();
                })
        };

    private static IReadOnlyDictionary<string, TryPatchCompositeKeySpec> BuildCompositeKeySuccessSpecs()
        => new Dictionary<string, TryPatchCompositeKeySpec>
        {
            ["scalar-fields"] = new(
                SeedEntity: () => CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "Before"),
                BuildUpdates: () => new Dictionary<string, object>
                {
                    ["Rating"] = 5,
                    ["Comment"] = "After",
                    ["FinishedAt"] = TimeSpan.FromHours(12)
                },
                AssertPersisted: entity =>
                {
                    entity.Rating.ShouldBe(5);
                    entity.Comment.ShouldBe("After");
                    entity.FinishedAt.ShouldBe(TimeSpan.FromHours(12));
                }),

            ["nullable-fields"] = new(
                SeedEntity: () => CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 3, comment: "HasComment", addedAt: new TimeOnly(15, 30)),
                BuildUpdates: () => new Dictionary<string, object>
                {
                    ["Comment"] = null!,
                    ["AddedAt"] = null!
                },
                AssertPersisted: entity =>
                {
                    entity.Comment.ShouldBeNull();
                    entity.AddedAt.ShouldBeNull();
                })
        };
}
