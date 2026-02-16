namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.ExecuteAsyncTests;

public partial class ExecuteAsyncTests
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
        string? sku = null)
    {
        var entityId = id ?? Guid.NewGuid();
        var token = entityId.ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        return new Product
        {
            Id = entityId,
            StoreId = storeId ?? DataSeeder.storeId,
            Name = name ?? $"ExecuteProduct-{token}",
            Sku = sku ?? $"EXE-{token}",
            Price = 42m,
            AddedIn = new DateOnly(2026, 11, 25),
            AddedAt = new TimeOnly(9, 30),
            FinishedAt = TimeSpan.FromHours(6),
            DiscontinuedAt = null,
            StockQuantity = 7,
            Weight = 1.3m,
            Count = 2,
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
        string? comment = "Execute review")
    {
        return new Review
        {
            ProductId = productId,
            CustomerId = customerId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
            AddedIn = new DateOnly(2026, 11, 25),
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

    protected async Task<Product?> FindProductAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Products.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
    }

    protected async Task<Review?> FindReviewAsync(Guid productId, Guid customerId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Reviews.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ProductId == productId && x.CustomerId == customerId);
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
