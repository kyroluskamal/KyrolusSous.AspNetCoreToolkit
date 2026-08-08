using System.Linq.Expressions;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllCompiledAsyncTests;

public partial class GetAllCompiledAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private sealed record CompiledSpec<TEntity>(
        Expression<Func<TEntity, bool>> Filter,
        int ExpectedCount,
        Action<List<TEntity>> Assert);

    private static readonly IReadOnlyDictionary<string, CompiledSpec<Product>> SingleKeySpecs = BuildSingleKeySpecs();
    private static readonly IReadOnlyDictionary<string, CompiledSpec<Review>> CompositeKeySpecs = BuildCompositeKeySpecs();

    public static TheoryData<string> SingleKeyCases => CaseIdsFrom(SingleKeySpecs);
    public static TheoryData<string> CompositeKeyCases => CaseIdsFrom(CompositeKeySpecs);

    [Theory(DisplayName = "GetAllCompiledAsync returns expected single-key results")]
    [MemberData(nameof(SingleKeyCases))]
    public async Task GetAllCompiledAsync_SingleKey_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SingleKeySpecs[caseId];

        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        var items = await repo.GetAllCompiledAsync(spec.Filter);
        items.Count.ShouldBe(spec.ExpectedCount);
        spec.Assert(items);
    }

    [Theory(DisplayName = "GetAllCompiledAsync returns expected composite-key results")]
    [MemberData(nameof(CompositeKeyCases))]
    public async Task GetAllCompiledAsync_CompositeKey_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = CompositeKeySpecs[caseId];

        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();

        var items = await repo.GetAllCompiledAsync(spec.Filter);
        items.Count.ShouldBe(spec.ExpectedCount);
        spec.Assert(items);
    }

    [Theory(DisplayName = "GetAllCompiledAsync rejects invalid trivial filters")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAllCompiledAsync_TrueFilter_Throws(bool compositeKey)
    {
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            Expression<Func<Review, bool>> filter = _ => true;
            await Should.ThrowAsync<ArgumentException>(async () => await repo.GetAllCompiledAsync(filter));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        Expression<Func<Product, bool>> singleFilter = _ => true;
        await Should.ThrowAsync<ArgumentException>(async () => await singleRepo.GetAllCompiledAsync(singleFilter));
    }

    [Theory(DisplayName = "GetAllCompiledAsync rejects null filters")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAllCompiledAsync_NullFilter_Throws(bool compositeKey)
    {
        using var scope = Factory.Services.CreateScope();

        if (compositeKey)
        {
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            await Should.ThrowAsync<ArgumentException>(async () => await repo.GetAllCompiledAsync(null!));
            return;
        }

        var singleRepo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        await Should.ThrowAsync<ArgumentException>(async () => await singleRepo.GetAllCompiledAsync(null!));
    }

    private static IReadOnlyDictionary<string, CompiledSpec<Product>> BuildSingleKeySpecs()
        => new Dictionary<string, CompiledSpec<Product>>
        {
            ["price-gt-100"] = new(
                Filter: p => p.Price > 100m,
                ExpectedCount: 2,
                Assert: items => items.All(x => x.Price > 100m).ShouldBeTrue()),

            ["price-between-100-and-1200"] = new(
                Filter: p => p.Price >= 100m && p.Price <= 1200m,
                ExpectedCount: 2,
                Assert: items => items.Select(x => x.Price).OrderBy(x => x).ShouldBe([199m, 1200m])),

            ["stock-gte-50"] = new(
                Filter: p => p.StockQuantity >= 50,
                ExpectedCount: 2,
                Assert: items => items.All(x => x.StockQuantity >= 50).ShouldBeTrue()),

            ["id-laptop"] = new(
                Filter: p => p.Id == DataSeeder.productLaptopId,
                ExpectedCount: 1,
                Assert: items => items.Single().Id.ShouldBe(DataSeeder.productLaptopId)),

            ["name-contains-code"] = new(
                Filter: p => p.Name.Contains("Code"),
                ExpectedCount: 1,
                Assert: items => items.Single().Name.ShouldBe("Clean Code")),

            ["addedin-year-2024"] = new(
                Filter: p => p.AddedIn.Year == 2024,
                ExpectedCount: 2,
                Assert: items => items.All(x => x.AddedIn.Year == 2024).ShouldBeTrue()),

            ["addedat-after-10"] = new(
                Filter: p => p.AddedAt.HasValue && p.AddedAt.Value > new TimeOnly(10, 0),
                ExpectedCount: 2,
                Assert: items => items.All(x => x.AddedAt > new TimeOnly(10, 0)).ShouldBeTrue()),

            ["createdat-2024"] = new(
                Filter: p => p.CreatedAt.Year == 2024,
                ExpectedCount: 2,
                Assert: items => items.All(x => x.CreatedAt.Year == 2024).ShouldBeTrue()),

            ["finishedat-day1"] = new(
                Filter: p => p.FinishedAt == TimeSpan.FromDays(1),
                ExpectedCount: 2,
                Assert: items => items.All(x => x.FinishedAt == TimeSpan.FromDays(1)).ShouldBeTrue()),

            ["weight-null"] = new(
                Filter: p => p.Weight == null,
                ExpectedCount: 1,
                Assert: items => items.Single().Id.ShouldBe(DataSeeder.productLaptopId)),

            ["count-null"] = new(
                Filter: p => p.Count == null,
                ExpectedCount: 1,
                Assert: items => items.Single().Id.ShouldBe(DataSeeder.productBookId)),

            ["categories-any-electronics"] = new(
                Filter: p => p.ProductCategories.Any(pc => pc.CategoryId == DataSeeder.categoryElectronicsId),
                ExpectedCount: 2,
                Assert: items =>
                {
                    items.Select(x => x.Id).OrderBy(x => x).ShouldBe([
                        DataSeeder.productLaptopId,
                        DataSeeder.productHeadphonesId
                    ]);
                }),

            ["reviews-any-gte4"] = new(
                Filter: p => p.Reviews.Any(r => r.Rating >= 4),
                ExpectedCount: 2,
                Assert: items =>
                {
                    items.Select(x => x.Name).OrderBy(x => x).ShouldBe([
                        "Clean Code",
                        "Laptop Pro 15"
                    ]);
                }),

            ["reviews-all-gte4"] = new(
                Filter: p => p.Reviews.All(r => r.Rating >= 4),
                ExpectedCount: 2,
                Assert: items =>
                {
                    items.Select(x => x.Name).OrderBy(x => x).ShouldBe([
                        "Clean Code",
                        "Laptop Pro 15"
                    ]);
                }),

            ["constant-false"] = new(
                Filter: _ => false,
                ExpectedCount: 0,
                Assert: items => items.Count.ShouldBe(0))
        };

    private static IReadOnlyDictionary<string, CompiledSpec<Review>> BuildCompositeKeySpecs()
        => new Dictionary<string, CompiledSpec<Review>>
        {
            ["rating-gte-4"] = new(
                Filter: r => r.Rating >= 4,
                ExpectedCount: 2,
                Assert: items => items.All(x => x.Rating >= 4).ShouldBeTrue()),

            ["rating-eq-3"] = new(
                Filter: r => r.Rating == 3,
                ExpectedCount: 1,
                Assert: items => items.Single().ProductId.ShouldBe(DataSeeder.productHeadphonesId)),

            ["customer-jane"] = new(
                Filter: r => r.CustomerId == DataSeeder.customerJaneId,
                ExpectedCount: 2,
                Assert: items => items.All(x => x.CustomerId == DataSeeder.customerJaneId).ShouldBeTrue()),

            ["product-laptop"] = new(
                Filter: r => r.ProductId == DataSeeder.productLaptopId,
                ExpectedCount: 1,
                Assert: items => items.Single().CustomerId.ShouldBe(DataSeeder.customerJaneId)),

            ["comment-contains-solid"] = new(
                Filter: r => r.Comment != null && r.Comment.Contains("Solid"),
                ExpectedCount: 1,
                Assert: items => items.Single().ProductId.ShouldBe(DataSeeder.productBookId)),

            ["comment-not-null"] = new(
                Filter: r => r.Comment != null,
                ExpectedCount: 3,
                Assert: items => items.All(x => x.Comment is not null).ShouldBeTrue()),

            ["addedin-year-2024"] = new(
                Filter: r => r.AddedIn.Year == 2024,
                ExpectedCount: 2,
                Assert: items => items.All(x => x.AddedIn.Year == 2024).ShouldBeTrue()),

            ["addedat-after-10"] = new(
                Filter: r => r.AddedAt.HasValue && r.AddedAt.Value > new TimeOnly(10, 0),
                ExpectedCount: 2,
                Assert: items => items.All(x => x.AddedAt > new TimeOnly(10, 0)).ShouldBeTrue()),

            ["createdat-gte-feb"] = new(
                Filter: r => r.CreatedAt >= new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero),
                ExpectedCount: 2,
                Assert: items => items.All(x => x.CreatedAt >= new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero)).ShouldBeTrue()),

            ["finishedat-day1"] = new(
                Filter: r => r.FinishedAt == TimeSpan.FromDays(1),
                ExpectedCount: 2,
                Assert: items => items.All(x => x.FinishedAt == TimeSpan.FromDays(1)).ShouldBeTrue()),

            ["deletedat-null"] = new(
                Filter: r => r.DeletedAt == null,
                ExpectedCount: 3,
                Assert: items => items.All(x => x.DeletedAt is null).ShouldBeTrue()),

            ["isdeleted-false"] = new(
                Filter: r => !r.IsDeleted,
                ExpectedCount: 3,
                Assert: items => items.All(x => !x.IsDeleted).ShouldBeTrue()),

            ["product-price-gt-100"] = new(
                Filter: r => r.Product != null && r.Product.Price > 100m,
                ExpectedCount: 2,
                Assert: items =>
                {
                    items.Select(x => x.ProductId).OrderBy(x => x).ShouldBe([
                        DataSeeder.productLaptopId,
                        DataSeeder.productHeadphonesId
                    ]);
                }),

            ["constant-false"] = new(
                Filter: _ => false,
                ExpectedCount: 0,
                Assert: items => items.Count.ShouldBe(0))
        };
}
