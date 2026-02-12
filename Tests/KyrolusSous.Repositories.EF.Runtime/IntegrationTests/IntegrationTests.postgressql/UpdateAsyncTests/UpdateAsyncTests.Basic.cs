namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.UpdateAsyncTests;

public partial class UpdateAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private sealed record UpdateSingleKeySpec(
        Func<Product> Seed,
        Action<Product> Mutate,
        Action<Product, Product> AssertPersisted,
        string? RouteId = null);

    private sealed record UpdateCompositeKeySpec(
        Func<Review> Seed,
        Action<Review> Mutate,
        Action<Review, Review> AssertPersisted,
        string? RouteId = null);

    private static readonly IReadOnlyDictionary<string, UpdateSingleKeySpec> SingleKeySuccessSpecs = BuildSingleKeySuccessSpecs();
    private static readonly IReadOnlyDictionary<string, UpdateCompositeKeySpec> CompositeKeySuccessSpecs = BuildCompositeKeySuccessSpecs();

    public static TheoryData<string> SingleKeySuccessCases => CaseIdsFrom(SingleKeySuccessSpecs);
    public static TheoryData<string> CompositeKeySuccessCases => CaseIdsFrom(CompositeKeySuccessSpecs);

    [Theory(DisplayName = "UpdateAsync updates single-key entities")]
    [MemberData(nameof(SingleKeySuccessCases))]
    public async Task UpdateAsync_SingleKey_UpdatesEntity(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SingleKeySuccessSpecs[caseId];
        var seed = spec.Seed();
        await SeedProductAsync(seed);

        try
        {
            var updated = Clone(seed);
            spec.Mutate(updated);

            var (response, content) = await PutEntityAsync<Product>(updated, spec.RouteId);
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent, content);
            content.ShouldBeEmpty();

            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == seed.Id);
            persisted.ShouldNotBeNull();
            spec.AssertPersisted(seed, persisted!);
        }
        finally
        {
            await CleanupProductAsync(seed.Id);
        }
    }

    [Theory(DisplayName = "UpdateAsync updates composite-key entities")]
    [MemberData(nameof(CompositeKeySuccessCases))]
    public async Task UpdateAsync_CompositeKey_UpdatesEntity(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = CompositeKeySuccessSpecs[caseId];
        var seed = spec.Seed();
        await SeedReviewAsync(seed);

        try
        {
            var updated = Clone(seed);
            spec.Mutate(updated);

            var (response, content) = await PutEntityAsync<Review>(updated, spec.RouteId);
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent, content);
            content.ShouldBeEmpty();

            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await db.Reviews.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ProductId == seed.ProductId && x.CustomerId == seed.CustomerId);
            persisted.ShouldNotBeNull();
            spec.AssertPersisted(seed, persisted!);
        }
        finally
        {
            await CleanupReviewAsync(seed.ProductId, seed.CustomerId);
        }
    }

    private static IReadOnlyDictionary<string, UpdateSingleKeySpec> BuildSingleKeySuccessSpecs()
        => new Dictionary<string, UpdateSingleKeySpec>
        {
            ["scalar-fields"] = new(
                Seed: () => CreateValidProduct(price: 20m, stockQuantity: 2, weight: 1.5m, count: 8, addedAt: new TimeOnly(9, 0)),
                Mutate: entity =>
                {
                    entity.Name = "Updated Name";
                    entity.Price = 88m;
                    entity.StockQuantity = 99;
                },
                AssertPersisted: (_, persisted) =>
                {
                    persisted.Name.ShouldBe("Updated Name");
                    persisted.Price.ShouldBe(88m);
                    persisted.StockQuantity.ShouldBe(99);
                }),

            ["nullable-to-null"] = new(
                Seed: () => CreateValidProduct(weight: 1.2m, count: 7, addedAt: new TimeOnly(15, 0)),
                Mutate: entity =>
                {
                    entity.Weight = null;
                    entity.Count = null;
                    entity.AddedAt = null;
                },
                AssertPersisted: (_, persisted) =>
                {
                    persisted.Weight.ShouldBeNull();
                    persisted.Count.ShouldBeNull();
                    persisted.AddedAt.ShouldBeNull();
                }),

            ["route-id-ignored"] = new(
                Seed: () => CreateValidProduct(name: "RouteId original"),
                Mutate: entity => entity.Name = "RouteId changed",
                AssertPersisted: (_, persisted) => persisted.Name.ShouldBe("RouteId changed"),
                RouteId: Guid.NewGuid().ToString())
        };

    private static IReadOnlyDictionary<string, UpdateCompositeKeySpec> BuildCompositeKeySuccessSpecs()
        => new Dictionary<string, UpdateCompositeKeySpec>
        {
            ["scalar-fields"] = new(
                Seed: () => CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 2, comment: "Old", addedAt: new TimeOnly(8, 0)),
                Mutate: entity =>
                {
                    entity.Rating = 5;
                    entity.Comment = "Updated";
                    entity.FinishedAt = TimeSpan.FromHours(20);
                },
                AssertPersisted: (_, persisted) =>
                {
                    persisted.Rating.ShouldBe(5);
                    persisted.Comment.ShouldBe("Updated");
                    persisted.FinishedAt.ShouldBe(TimeSpan.FromHours(20));
                }),

            ["nullable-to-null"] = new(
                Seed: () => CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 4, comment: "Has comment", addedAt: new TimeOnly(10, 30)),
                Mutate: entity =>
                {
                    entity.Comment = null;
                    entity.AddedAt = null;
                },
                AssertPersisted: (_, persisted) =>
                {
                    persisted.Comment.ShouldBeNull();
                    persisted.AddedAt.ShouldBeNull();
                })
        };
}

