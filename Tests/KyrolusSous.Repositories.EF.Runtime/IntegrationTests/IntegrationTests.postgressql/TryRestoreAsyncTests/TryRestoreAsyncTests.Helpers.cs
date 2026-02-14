namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryRestoreAsyncTests;

public partial class TryRestoreAsyncTests
{
    private static readonly IReadOnlyDictionary<string, bool> KeyTypeSpecs = BuildKeyTypeSpecs();
    public static TheoryData<string> KeyTypeCases => CaseIdsFrom(KeyTypeSpecs);

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
            Name = name ?? $"TryRestoreProduct-{token}",
            Sku = sku ?? $"TRS-{token}",
            Price = 60m,
            AddedIn = new DateOnly(2026, 9, 1),
            AddedAt = new TimeOnly(10, 0),
            FinishedAt = TimeSpan.FromHours(7),
            DiscontinuedAt = null,
            StockQuantity = 8,
            Weight = 0.5m,
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
        string? comment = "Try restore review")
    {
        return new Review
        {
            ProductId = productId,
            CustomerId = customerId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
            AddedIn = new DateOnly(2026, 9, 1),
            AddedAt = new TimeOnly(9, 30),
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

    protected async Task SoftDeleteProductAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        var deleted = await repo.SoftDeleteAsync(id);
        deleted.ShouldBeTrue();
        (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
    }

    protected async Task SoftDeleteReviewAsync(Guid productId, Guid customerId)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>>();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();
        var deleted = await repo.SoftDeleteAsync([productId, customerId]);
        deleted.ShouldBeTrue();
        (await uow.SaveChangesAsync()).ShouldBeGreaterThan(0);
    }

    protected async Task<bool?> IsProductDeletedAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Products.IgnoreQueryFilters()
            .Where(x => x.Id == id)
            .Select(x => (bool?)x.IsDeleted)
            .SingleOrDefaultAsync();
    }

    protected async Task<bool?> IsReviewDeletedAsync(Guid productId, Guid customerId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Reviews.IgnoreQueryFilters()
            .Where(x => x.ProductId == productId && x.CustomerId == customerId)
            .Select(x => (bool?)x.IsDeleted)
            .SingleOrDefaultAsync();
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

    private static IReadOnlyDictionary<string, bool> BuildKeyTypeSpecs()
        => new Dictionary<string, bool>
        {
            ["single-key"] = false,
            ["composite-key"] = true
        };
}
