namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryUpdateAsyncTests;

public partial class TryUpdateAsyncTests
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
        decimal price = 129.5m,
        int stockQuantity = 9,
        decimal? weight = 1.4m,
        int? count = 6,
        TimeOnly? addedAt = null)
    {
        var entityId = id ?? Guid.NewGuid();
        var token = entityId.ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        return new Product
        {
            Id = entityId,
            StoreId = storeId ?? DataSeeder.storeId,
            Name = name ?? $"TryUpdateProduct-{token}",
            Sku = sku ?? $"TUP-{token}",
            Price = price,
            AddedIn = new DateOnly(2026, 1, 10),
            AddedAt = addedAt,
            FinishedAt = TimeSpan.FromHours(8),
            DiscontinuedAt = null,
            StockQuantity = stockQuantity,
            Weight = weight,
            Count = count,
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
        string? comment = "TryUpdate review",
        TimeOnly? addedAt = null)
    {
        return new Review
        {
            ProductId = productId,
            CustomerId = customerId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
            AddedIn = new DateOnly(2026, 1, 10),
            AddedAt = addedAt,
            FinishedAt = TimeSpan.FromHours(3),
            DiscontinuedAt = null,
            IsDeleted = false,
            DeletedAt = null
        };
    }

    protected static Product Clone(Product source)
        => new()
        {
            Id = source.Id,
            StoreId = source.StoreId,
            Name = source.Name,
            Sku = source.Sku,
            Price = source.Price,
            AddedIn = source.AddedIn,
            AddedAt = source.AddedAt,
            FinishedAt = source.FinishedAt,
            DiscontinuedAt = source.DiscontinuedAt,
            StockQuantity = source.StockQuantity,
            Weight = source.Weight,
            Count = source.Count,
            IsActive = source.IsActive,
            RowVersion = source.RowVersion is null ? null : [.. source.RowVersion],
            IsDeleted = source.IsDeleted,
            DeletedAt = source.DeletedAt,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        };

    protected static Review Clone(Review source)
        => new()
        {
            ProductId = source.ProductId,
            CustomerId = source.CustomerId,
            Rating = source.Rating,
            Comment = source.Comment,
            CreatedAt = source.CreatedAt,
            AddedIn = source.AddedIn,
            AddedAt = source.AddedAt,
            FinishedAt = source.FinishedAt,
            DiscontinuedAt = source.DiscontinuedAt,
            IsDeleted = source.IsDeleted,
            DeletedAt = source.DeletedAt
        };

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

    protected async Task<Product?> FindProductAsync(Guid id, bool ignoreFilters = false)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var query = ignoreFilters ? db.Products.IgnoreQueryFilters() : db.Products.AsQueryable();
        return await query.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
    }

    protected async Task<Review?> FindReviewAsync(Guid productId, Guid customerId, bool ignoreFilters = false)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var query = ignoreFilters ? db.Reviews.IgnoreQueryFilters() : db.Reviews.AsQueryable();
        return await query.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == productId && x.CustomerId == customerId);
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
        var entity = await db.Products.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == id);
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
            .SingleOrDefaultAsync(x => x.ProductId == productId && x.CustomerId == customerId);
        if (entity is null)
            return;

        db.Reviews.Remove(entity);
        await db.SaveChangesAsync();
    }
}
