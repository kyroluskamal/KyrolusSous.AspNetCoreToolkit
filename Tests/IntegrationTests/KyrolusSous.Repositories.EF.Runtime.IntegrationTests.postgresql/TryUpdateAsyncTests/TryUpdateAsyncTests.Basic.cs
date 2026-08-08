namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryUpdateAsyncTests;

public partial class TryUpdateAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private sealed record TryUpdateSingleKeySpec(
        Func<Product> SeedEntity,
        Action<Product> MutateEntity,
        Action<Product> AssertState);

    private sealed record TryUpdateCompositeKeySpec(
        Func<Review> SeedEntity,
        Action<Review> MutateEntity,
        Action<Review> AssertState);

    private static readonly IReadOnlyDictionary<string, TryUpdateSingleKeySpec> SingleKeySuccessSpecs = BuildSingleKeySuccessSpecs();
    private static readonly IReadOnlyDictionary<string, TryUpdateCompositeKeySpec> CompositeKeySuccessSpecs = BuildCompositeKeySuccessSpecs();

    public static TheoryData<string> SingleKeySuccessCases => CaseIdsFrom(SingleKeySuccessSpecs);
    public static TheoryData<string> CompositeKeySuccessCases => CaseIdsFrom(CompositeKeySuccessSpecs);

    [Theory(DisplayName = "TryUpdateAsync succeeds for single-key entities")]
    [MemberData(nameof(SingleKeySuccessCases))]
    public async Task TryUpdateAsync_SingleKey_Success(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SingleKeySuccessSpecs[caseId];
        var seed = spec.SeedEntity();
        await SeedProductAsync(seed);

        try
        {
            var updated = Clone(seed);
            spec.MutateEntity(updated);

            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
            var result = await repo.TryUpdateAsync(updated);

            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            result.Exception.ShouldBeNull();
            result.Value.ShouldNotBeNull();
            ReferenceEquals(updated, result.Value).ShouldBeFalse();
            spec.AssertState(result.Value!);

            var affected = await uow.SaveChangesAsync();
            affected.ShouldBeGreaterThan(0);

            var persisted = await FindProductAsync(seed.Id);
            persisted.ShouldNotBeNull();
            spec.AssertState(persisted!);
        }
        finally
        {
            await CleanupProductAsync(seed.Id);
        }
    }

    [Theory(DisplayName = "TryUpdateAsync succeeds for composite-key entities")]
    [MemberData(nameof(CompositeKeySuccessCases))]
    public async Task TryUpdateAsync_CompositeKey_Success(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = CompositeKeySuccessSpecs[caseId];
        var seed = spec.SeedEntity();
        await SeedReviewAsync(seed);

        try
        {
            var updated = Clone(seed);
            spec.MutateEntity(updated);

            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
            var result = await repo.TryUpdateAsync(updated);

            result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
            result.Exception.ShouldBeNull();
            result.Value.ShouldNotBeNull();
            ReferenceEquals(updated, result.Value).ShouldBeFalse();
            spec.AssertState(result.Value!);

            var affected = await uow.SaveChangesAsync();
            affected.ShouldBeGreaterThan(0);

            var persisted = await FindReviewAsync(seed.ProductId, seed.CustomerId);
            persisted.ShouldNotBeNull();
            spec.AssertState(persisted!);
        }
        finally
        {
            await CleanupReviewAsync(seed.ProductId, seed.CustomerId);
        }
    }

    private static IReadOnlyDictionary<string, TryUpdateSingleKeySpec> BuildSingleKeySuccessSpecs()
        => new Dictionary<string, TryUpdateSingleKeySpec>
        {
            ["scalar-fields"] = new(
                SeedEntity: () => CreateValidProduct(name: "Before-Single", price: 33m, stockQuantity: 5),
                MutateEntity: entity =>
                {
                    entity.Name = "After-Single";
                    entity.Price = 75m;
                    entity.StockQuantity = 44;
                    entity.IsActive = false;
                },
                AssertState: entity =>
                {
                    entity.Name.ShouldBe("After-Single");
                    entity.Price.ShouldBe(75m);
                    entity.StockQuantity.ShouldBe(44);
                    entity.IsActive.ShouldBeFalse();
                }),

            ["nullable-fields"] = new(
                SeedEntity: () => CreateValidProduct(weight: 1.2m, count: 5, addedAt: new TimeOnly(10, 30)),
                MutateEntity: entity =>
                {
                    entity.Weight = null;
                    entity.Count = null;
                    entity.AddedAt = null;
                },
                AssertState: entity =>
                {
                    entity.Weight.ShouldBeNull();
                    entity.Count.ShouldBeNull();
                    entity.AddedAt.ShouldBeNull();
                })
        };

    private static IReadOnlyDictionary<string, TryUpdateCompositeKeySpec> BuildCompositeKeySuccessSpecs()
        => new Dictionary<string, TryUpdateCompositeKeySpec>
        {
            ["scalar-fields"] = new(
                SeedEntity: () => CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 2, comment: "Before"),
                MutateEntity: entity =>
                {
                    entity.Rating = 5;
                    entity.Comment = "After";
                    entity.FinishedAt = TimeSpan.FromHours(9);
                },
                AssertState: entity =>
                {
                    entity.Rating.ShouldBe(5);
                    entity.Comment.ShouldBe("After");
                    entity.FinishedAt.ShouldBe(TimeSpan.FromHours(9));
                }),

            ["nullable-fields"] = new(
                SeedEntity: () => CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 4, comment: "HasComment", addedAt: new TimeOnly(13, 15)),
                MutateEntity: entity =>
                {
                    entity.Comment = null;
                    entity.AddedAt = null;
                },
                AssertState: entity =>
                {
                    entity.Comment.ShouldBeNull();
                    entity.AddedAt.ShouldBeNull();
                })
        };
}
