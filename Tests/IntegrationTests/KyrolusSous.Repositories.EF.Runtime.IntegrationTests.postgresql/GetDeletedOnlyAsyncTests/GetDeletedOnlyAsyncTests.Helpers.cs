namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetDeletedOnlyAsyncTests;

public partial class GetDeletedOnlyAsyncTests
{
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
        string? sku = null,
        decimal price = 44.5m,
        int stockQuantity = 8)
    {
        var entityId = id ?? Guid.NewGuid();
        var token = entityId.ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        return new Product
        {
            Id = entityId,
            StoreId = storeId ?? DataSeeder.storeId,
            Name = name ?? $"DeletedOnlyProduct-{token}",
            Sku = sku ?? $"GDO-{token}",
            Price = price,
            AddedIn = new DateOnly(2026, 3, 1),
            AddedAt = new TimeOnly(9, 0),
            FinishedAt = TimeSpan.FromHours(8),
            DiscontinuedAt = null,
            StockQuantity = stockQuantity,
            Weight = 1.0m,
            Count = 5,
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
        string? comment = "DeletedOnly review")
    {
        return new Review
        {
            ProductId = productId,
            CustomerId = customerId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
            AddedIn = new DateOnly(2026, 3, 1),
            AddedAt = new TimeOnly(10, 15),
            FinishedAt = TimeSpan.FromHours(4),
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

    protected async Task CleanupProductAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entity = await db.Products.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return;

        db.Products.Remove(entity);
        await db.SaveChangesAsync();
    }

    protected async Task CleanupProductsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.Distinct().ToArray();
        if (idList.Length == 0)
            return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entities = await db.Products.IgnoreQueryFilters()
            .Where(x => idList.Contains(x.Id))
            .ToListAsync();
        if (entities.Count == 0)
            return;

        db.Products.RemoveRange(entities);
        await db.SaveChangesAsync();
    }

    protected async Task CleanupReviewAsync(Guid productId, Guid customerId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entity = await db.Reviews.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.ProductId == productId && x.CustomerId == customerId);
        if (entity is null)
            return;

        db.Reviews.Remove(entity);
        await db.SaveChangesAsync();
    }

    protected async Task CleanupReviewsAsync(IEnumerable<(Guid ProductId, Guid CustomerId)> keys)
    {
        var keyList = keys.Distinct().ToArray();
        if (keyList.Length == 0)
            return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var key in keyList)
        {
            var entity = await db.Reviews.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.ProductId == key.ProductId && x.CustomerId == key.CustomerId);
            if (entity is not null)
                db.Reviews.Remove(entity);
        }

        await db.SaveChangesAsync();
    }
}
