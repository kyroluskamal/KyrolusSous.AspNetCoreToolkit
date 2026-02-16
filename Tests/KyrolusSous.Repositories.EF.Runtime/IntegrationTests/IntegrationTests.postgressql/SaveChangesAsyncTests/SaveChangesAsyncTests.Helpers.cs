namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.SaveChangesAsyncTests;

public partial class SaveChangesAsyncTests
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
            Name = name ?? $"SaveProduct-{token}",
            Sku = sku ?? $"SAV-{token}",
            Price = 39m,
            AddedIn = new DateOnly(2026, 12, 1),
            AddedAt = new TimeOnly(8, 45),
            FinishedAt = TimeSpan.FromHours(5),
            DiscontinuedAt = null,
            StockQuantity = 4,
            Weight = 0.9m,
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
        string? comment = "SaveChanges review")
    {
        return new Review
        {
            ProductId = productId,
            CustomerId = customerId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
            AddedIn = new DateOnly(2026, 12, 1),
            AddedAt = new TimeOnly(10, 0),
            FinishedAt = TimeSpan.FromHours(3),
            DiscontinuedAt = null,
            IsDeleted = false,
            DeletedAt = null
        };
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

    protected TestRepositoryObserver GetObserver()
        => Factory.Services.GetRequiredService<TestRepositoryObserver>();
}
