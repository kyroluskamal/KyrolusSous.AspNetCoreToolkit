using System.Linq.Expressions;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetDeletedOnlyAsyncTests;

public partial class GetDeletedOnlyAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private sealed record DeletedOnlySpec(bool UseIncludeExpressions, bool UseSplitQuery);

    private static readonly IReadOnlyDictionary<string, DeletedOnlySpec> SingleSpecs = BuildSingleSpecs();
    private static readonly IReadOnlyDictionary<string, DeletedOnlySpec> CompositeSpecs = BuildCompositeSpecs();
    private static readonly IReadOnlyDictionary<string, bool> KeyTypeSpecs = BuildKeyTypeSpecs();

    public static TheoryData<string> SingleCases => CaseIdsFrom(SingleSpecs);
    public static TheoryData<string> CompositeCases => CaseIdsFrom(CompositeSpecs);
    public static TheoryData<string> KeyTypeCases => CaseIdsFrom(KeyTypeSpecs);

    [Theory(DisplayName = "GetDeletedOnlyAsync returns only soft-deleted single-key entities")]
    [MemberData(nameof(SingleCases))]
    public async Task GetDeletedOnlyAsync_SingleKey_ReturnsOnlyDeleted(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = SingleSpecs[caseId];
        var token = Guid.NewGuid().ToString("N")[..8];
        var prefix = $"GDO-S-{token}-";
        var deletedEntity = CreateValidProduct(name: $"{prefix}deleted", sku: $"{prefix}d", price: 50m);
        var activeEntity = CreateValidProduct(name: $"{prefix}active", sku: $"{prefix}a", price: 70m);
        await SeedProductAsync(deletedEntity);
        await SeedProductAsync(activeEntity);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
            (await repo.SoftDeleteAsync(deletedEntity.Id)).ShouldBeTrue();
            (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);

            var filter = (Expression<Func<Product, bool>>)(x => x.Sku.StartsWith(prefix));
            var orderBy = (Func<IQueryable<Product>, IOrderedQueryable<Product>>)(q => q.OrderBy(x => x.Sku));

            IReadOnlyList<Product> items;
            if (spec.UseIncludeExpressions)
            {
                items = await repo.GetDeletedOnlyAsync(
                    filter,
                    orderBy,
                    asNoTracking: true,
                    useSplitQuery: spec.UseSplitQuery,
                    cancellationToken: default,
                    x => x.Store);
            }
            else
            {
                items = await repo.GetDeletedOnlyAsync(
                    filter,
                    orderBy,
                    includeProperties: ["Store"],
                    includeGraph: null,
                    asNoTracking: true,
                    useSplitQuery: spec.UseSplitQuery);
            }

            items.Count.ShouldBe(1);
            items[0].Id.ShouldBe(deletedEntity.Id);
            items[0].IsDeleted.ShouldBeTrue();
            items[0].Store.ShouldNotBeNull();
        }
        finally
        {
            await CleanupProductsAsync([deletedEntity.Id, activeEntity.Id]);
        }
    }

    [Theory(DisplayName = "GetDeletedOnlyAsync returns only soft-deleted composite-key entities")]
    [MemberData(nameof(CompositeCases))]
    public async Task GetDeletedOnlyAsync_CompositeKey_ReturnsOnlyDeleted(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = CompositeSpecs[caseId];
        var token = Guid.NewGuid().ToString("N")[..8];
        var prefix = $"GDO-C-{token}-";
        var deletedEntity = CreateValidReview(
            DataSeeder.productBookId,
            DataSeeder.customerJohnId,
            rating: 2,
            comment: $"{prefix}deleted");
        var activeEntity = CreateValidReview(
            DataSeeder.productLaptopId,
            DataSeeder.customerJohnId,
            rating: 5,
            comment: $"{prefix}active");
        await SeedReviewAsync(deletedEntity);
        await SeedReviewAsync(activeEntity);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
            (await repo.SoftDeleteAsync([deletedEntity.ProductId, deletedEntity.CustomerId])).ShouldBeTrue();
            (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);

            var filter = (Expression<Func<Review, bool>>)(x => x.Comment != null && x.Comment.StartsWith(prefix));
            var orderBy = (Func<IQueryable<Review>, IOrderedQueryable<Review>>)(q => q.OrderBy(x => x.Comment));

            IReadOnlyList<Review> items;
            if (spec.UseIncludeExpressions)
            {
                items = await repo.GetDeletedOnlyAsync(
                    filter,
                    orderBy,
                    asNoTracking: true,
                    useSplitQuery: spec.UseSplitQuery,
                    cancellationToken: default,
                    x => x.Product);
            }
            else
            {
                items = await repo.GetDeletedOnlyAsync(
                    filter,
                    orderBy,
                    includeProperties: ["Product"],
                    includeGraph: null,
                    asNoTracking: true,
                    useSplitQuery: spec.UseSplitQuery);
            }

            items.Count.ShouldBe(1);
            items[0].ProductId.ShouldBe(deletedEntity.ProductId);
            items[0].CustomerId.ShouldBe(deletedEntity.CustomerId);
            items[0].IsDeleted.ShouldBeTrue();
            items[0].Product.ShouldNotBeNull();
        }
        finally
        {
            await CleanupReviewsAsync(
            [
                (deletedEntity.ProductId, deletedEntity.CustomerId),
                (activeEntity.ProductId, activeEntity.CustomerId)
            ]);
        }
    }

    [Theory(DisplayName = "GetDeletedOnlyAsync applies ordering on deleted rows")]
    [MemberData(nameof(KeyTypeCases))]
    public async Task GetDeletedOnlyAsync_OrderBy_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var compositeKey = KeyTypeSpecs[caseId];

        if (compositeKey)
        {
            var first = CreateValidReview(DataSeeder.productBookId, DataSeeder.customerJohnId, rating: 1, comment: "order-c-1");
            var second = CreateValidReview(DataSeeder.productHeadphonesId, DataSeeder.customerJaneId, rating: 5, comment: "order-c-2");
            await SeedReviewAsync(first);
            await SeedReviewAsync(second);

            try
            {
                using var scope = Factory.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
                var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
                (await repo.SoftDeleteAsync([first.ProductId, first.CustomerId])).ShouldBeTrue();
                (await repo.SoftDeleteAsync([second.ProductId, second.CustomerId])).ShouldBeTrue();
                (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);

                var items = await repo.GetDeletedOnlyAsync(
                    x => x.Comment != null && x.Comment.StartsWith("order-c-"),
                    q => q.OrderByDescending(x => x.Rating));

                items.Count.ShouldBe(2);
                items[0].Rating.ShouldBe(5);
                items[1].Rating.ShouldBe(1);
            }
            finally
            {
                await CleanupReviewsAsync([(first.ProductId, first.CustomerId), (second.ProductId, second.CustomerId)]);
            }

            return;
        }

        var p1 = CreateValidProduct(name: "order-s-1", sku: $"order-s-1-{Guid.NewGuid():N}", price: 10m);
        var p2 = CreateValidProduct(name: "order-s-2", sku: $"order-s-2-{Guid.NewGuid():N}", price: 80m);
        await SeedProductAsync(p1);
        await SeedProductAsync(p2);

        try
        {
            using var scope = Factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
            var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
            (await repo.SoftDeleteAsync(p1.Id)).ShouldBeTrue();
            (await repo.SoftDeleteAsync(p2.Id)).ShouldBeTrue();
            (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);

            var items = await repo.GetDeletedOnlyAsync(
                x => x.Name.StartsWith("order-s-"),
                q => q.OrderByDescending(x => x.Price));

            items.Count.ShouldBe(2);
            items[0].Price.ShouldBe(80m);
            items[1].Price.ShouldBe(10m);
        }
        finally
        {
            await CleanupProductsAsync([p1.Id, p2.Id]);
        }
    }

    private static IReadOnlyDictionary<string, DeletedOnlySpec> BuildSingleSpecs()
        => new Dictionary<string, DeletedOnlySpec>
        {
            ["single-include-properties"] = new(UseIncludeExpressions: false, UseSplitQuery: false),
            ["single-include-expressions-split"] = new(UseIncludeExpressions: true, UseSplitQuery: true)
        };

    private static IReadOnlyDictionary<string, DeletedOnlySpec> BuildCompositeSpecs()
        => new Dictionary<string, DeletedOnlySpec>
        {
            ["composite-include-properties"] = new(UseIncludeExpressions: false, UseSplitQuery: false),
            ["composite-include-expressions-split"] = new(UseIncludeExpressions: true, UseSplitQuery: true)
        };

    private static IReadOnlyDictionary<string, bool> BuildKeyTypeSpecs()
        => new Dictionary<string, bool>
        {
            ["single-key"] = false,
            ["composite-key"] = true
        };
}
