using System.Linq.Expressions;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.QueryAsyncTests;

public partial class QueryAsyncTests
{
    private sealed class TestQuerySpecification<TEntity, TResult> : IKyrolusQuerySpecification<TEntity, TResult>, IKyrolusHasSplitQuery
    {
        public Expression<Func<TEntity, bool>>? Filter { get; init; }
        public required Expression<Func<TEntity, TResult>>? Selector { get; init; }
        public Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? OrderBy { get; init; }
        public Expression<Func<TEntity, object?>>[]? Includes { get; init; }
        public bool AsNoTracking { get; init; } = true;
        public bool IncludeDeleted { get; init; }
        public bool UseSplitQuery { get; init; }
    }

    private sealed class TestQuerySpecificationWithoutSplit<TEntity, TResult> : IKyrolusQuerySpecification<TEntity, TResult>
    {
        public Expression<Func<TEntity, bool>>? Filter { get; init; }
        public required Expression<Func<TEntity, TResult>>? Selector { get; init; }
        public Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? OrderBy { get; init; }
        public Expression<Func<TEntity, object?>>[]? Includes { get; init; }
        public bool AsNoTracking { get; init; } = true;
        public bool IncludeDeleted { get; init; }
    }

    protected sealed record ProductQueryProjection(Guid Id, string Sku, decimal Price, int ReviewsCount);
    protected sealed record ReviewQueryProjection(Guid ProductId, Guid CustomerId, int Rating, string? Comment);

    protected static TheoryData<string> CaseIdsFrom<TSpec>(IReadOnlyDictionary<string, TSpec> specs)
    {
        var data = new TheoryData<string>();
        foreach (var key in specs.Keys)
            data.Add(key);
        return data;
    }

    protected static Product CreateValidProduct(
        Guid? id = null,
        Guid? storeId = null,
        string? name = null,
        string? sku = null)
    {
        var entityId = id ?? Guid.NewGuid();
        var token = entityId.ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        return new Product
        {
            Id = entityId,
            StoreId = storeId ?? DataSeeder.storeId,
            Name = name ?? $"QueryProduct-{token}",
            Sku = sku ?? $"QRY-{token}",
            Price = 77m,
            AddedIn = new DateOnly(2026, 12, 1),
            AddedAt = new TimeOnly(10, 0),
            FinishedAt = TimeSpan.FromHours(8),
            DiscontinuedAt = null,
            StockQuantity = 8,
            Weight = 0.9m,
            Count = 3,
            IsActive = true,
            RowVersion = [0],
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    protected static Review CreateValidReview(
        Guid productId,
        Guid customerId,
        int rating = 4,
        string? comment = "Query review")
    {
        return new Review
        {
            ProductId = productId,
            CustomerId = customerId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
            AddedIn = new DateOnly(2026, 12, 1),
            AddedAt = new TimeOnly(9, 30),
            FinishedAt = TimeSpan.FromHours(6),
            DiscontinuedAt = null,
            IsDeleted = false,
            DeletedAt = null
        };
    }

    protected async Task SeedProductAsync(Product entity)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Products.Add(entity);
        await db.SaveChangesAsync();
    }

    protected async Task SeedReviewAsync(Review entity)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Reviews.Add(entity);
        await db.SaveChangesAsync();
    }

    protected async Task SoftDeleteProductAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        (await repo.SoftDeleteAsync(id)).ShouldBeTrue();
        (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
    }

    protected async Task SoftDeleteReviewAsync(Guid productId, Guid customerId)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        (await repo.SoftDeleteAsync([productId, customerId])).ShouldBeTrue();
        (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
    }

    protected async Task CleanupProductAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entity = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return;

        db.Products.Remove(entity);
        await db.SaveChangesAsync();
    }

    protected async Task CleanupReviewAsync(Guid productId, Guid customerId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entity = await db.Reviews.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.ProductId == productId && x.CustomerId == customerId);
        if (entity is null)
            return;

        db.Reviews.Remove(entity);
        await db.SaveChangesAsync();
    }
}
