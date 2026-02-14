namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.UpdateRangeAsyncTests;

public partial class UpdateRangeAsyncTests
{
    private sealed record UpdateRangeSingleKeySpec(
        Func<List<Product>> Seed,
        Action<List<Product>> Mutate,
        Action<List<Product>, List<Product>> AssertPersisted);

    private sealed record UpdateRangeCompositeKeySpec(
        Func<List<Review>> Seed,
        Action<List<Review>> Mutate,
        Action<List<Review>, List<Review>> AssertPersisted);

    private static readonly IReadOnlyDictionary<string, UpdateRangeSingleKeySpec> SingleKeySuccessSpecs = BuildSingleKeySuccessSpecs();
    private static readonly IReadOnlyDictionary<string, UpdateRangeCompositeKeySpec> CompositeKeySuccessSpecs = BuildCompositeKeySuccessSpecs();

    public static TheoryData<string> SingleKeySuccessCases => CaseIdsFrom(SingleKeySuccessSpecs);
    public static TheoryData<string> CompositeKeySuccessCases => CaseIdsFrom(CompositeKeySuccessSpecs);

    [Theory(DisplayName = "UpdateRangeAsync updates single-key entities")]
    [MemberData(nameof(SingleKeySuccessCases))]
    public async Task UpdateRangeAsync_SingleKey_UpdatesEntities(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SingleKeySuccessSpecs[caseId];
        var seed = spec.Seed();
        await SeedProductsAsync(seed);

        try
        {
            var updated = seed.Select(Clone).ToList();
            spec.Mutate(updated);

            using var writeScope = Factory.Services.CreateScope();
            var repo = writeScope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var uow = writeScope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
            var result = await repo.UpdateRangeAsync(updated);
            result.Count().ShouldBe(updated.Count);
            await uow.SaveChangesAsync();

            var ids = seed.Select(x => x.Id).ToArray();
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await db.Products.AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .OrderBy(x => x.Id)
                .ToListAsync();

            persisted.Count.ShouldBe(seed.Count);
            spec.AssertPersisted(seed.OrderBy(x => x.Id).ToList(), persisted);
        }
        finally
        {
            await CleanupProductsAsync(seed.Select(x => x.Id));
        }
    }

    [Theory(DisplayName = "UpdateRangeAsync updates composite-key entities")]
    [MemberData(nameof(CompositeKeySuccessCases))]
    public async Task UpdateRangeAsync_CompositeKey_UpdatesEntities(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = CompositeKeySuccessSpecs[caseId];
        var seed = spec.Seed();
        await SeedReviewsAsync(seed);

        try
        {
            var updated = seed.Select(Clone).ToList();
            spec.Mutate(updated);

            using var writeScope = Factory.Services.CreateScope();
            var repo = writeScope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var uow = writeScope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
            var result = await repo.UpdateRangeAsync(updated);
            result.Count().ShouldBe(updated.Count);
            await uow.SaveChangesAsync();

            var keys = seed.Select(x => (x.ProductId, x.CustomerId)).ToList();
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var persisted = await db.Reviews.AsNoTracking()
                .Where(x => keys.Select(k => k.ProductId).Contains(x.ProductId) && keys.Select(k => k.CustomerId).Contains(x.CustomerId))
                .ToListAsync();

            persisted = persisted
                .Where(x => keys.Any(k => k.ProductId == x.ProductId && k.CustomerId == x.CustomerId))
                .OrderBy(x => x.ProductId)
                .ThenBy(x => x.CustomerId)
                .ToList();

            persisted.Count.ShouldBe(seed.Count);
            spec.AssertPersisted(seed.OrderBy(x => x.ProductId).ThenBy(x => x.CustomerId).ToList(), persisted);
        }
        finally
        {
            await CleanupReviewsAsync(seed.Select(x => (x.ProductId, x.CustomerId)));
        }
    }

    private static IReadOnlyDictionary<string, UpdateRangeSingleKeySpec> BuildSingleKeySuccessSpecs()
        => new Dictionary<string, UpdateRangeSingleKeySpec>
        {
            ["scalar-fields"] = new(
                Seed: () =>
                [
                    CreateValidProduct(price: 19m, stockQuantity: 3, weight: 0.4m, count: 2, addedAt: new TimeOnly(9, 0)),
                    CreateValidProduct(price: 39m, stockQuantity: 5, weight: 0.8m, count: 4, addedAt: new TimeOnly(10, 0))
                ],
                Mutate: entities =>
                {
                    entities[0].Name = "Updated range A";
                    entities[0].Price = 99m;
                    entities[1].Name = "Updated range B";
                    entities[1].StockQuantity = 77;
                },
                AssertPersisted: (_, persisted) =>
                {
                    persisted.Select(x => x.Name).OrderBy(x => x).ShouldBe(["Updated range A", "Updated range B"]);
                    persisted.Select(x => x.Price).ShouldContain(99m);
                    persisted.Select(x => x.StockQuantity).ShouldContain(77);
                }),

            ["nullable-fields"] = new(
                Seed: () =>
                [
                    CreateValidProduct(weight: 1.1m, count: 5, addedAt: new TimeOnly(11, 0)),
                    CreateValidProduct(weight: 2.2m, count: 6, addedAt: new TimeOnly(12, 0))
                ],
                Mutate: entities =>
                {
                    entities[0].Weight = null;
                    entities[0].Count = null;
                    entities[1].AddedAt = null;
                },
                AssertPersisted: (_, persisted) =>
                {
                    persisted.Count(x => x.Weight is null).ShouldBe(1);
                    persisted.Count(x => x.Count is null).ShouldBe(1);
                    persisted.Count(x => x.AddedAt is null).ShouldBe(1);
                })
        };

    private static IReadOnlyDictionary<string, UpdateRangeCompositeKeySpec> BuildCompositeKeySuccessSpecs()
        => new Dictionary<string, UpdateRangeCompositeKeySpec>
        {
            ["scalar-fields"] = new(
                Seed: () =>
                [
                    CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 2, comment: "Old A"),
                    CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 3, comment: "Old B")
                ],
                Mutate: entities =>
                {
                    entities[0].Rating = 5;
                    entities[0].Comment = "Updated A";
                    entities[1].Rating = 1;
                    entities[1].FinishedAt = TimeSpan.FromHours(12);
                },
                AssertPersisted: (_, persisted) =>
                {
                    persisted.Select(x => x.Rating).ShouldBe([5, 1], ignoreOrder: true);
                    persisted.Any(x => x.Comment == "Updated A").ShouldBeTrue();
                    persisted.Any(x => x.FinishedAt == TimeSpan.FromHours(12)).ShouldBeTrue();
                }),

            ["nullable-fields"] = new(
                Seed: () =>
                [
                    CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 4, comment: "Has comment", addedAt: new TimeOnly(15, 0)),
                    CreateValidReview(DataSeeder.productLaptopId, DataSeeder.customerJohnId, rating: 2, comment: "Has comment too", addedAt: new TimeOnly(16, 0))
                ],
                Mutate: entities =>
                {
                    entities[0].Comment = null;
                    entities[0].AddedAt = null;
                    entities[1].Comment = null;
                },
                AssertPersisted: (_, persisted) =>
                {
                    persisted.Count(x => x.Comment is null).ShouldBe(2);
                    persisted.Count(x => x.AddedAt is null).ShouldBe(1);
                })
        };
}
