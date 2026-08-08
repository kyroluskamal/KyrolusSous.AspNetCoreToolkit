using System.Text;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.UpdateRangeAsyncTests;

public partial class UpdateRangeAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    protected static TheoryData<string> CaseIdsFrom<TSpec>(IReadOnlyDictionary<string, TSpec> specs)
    {
        var data = new TheoryData<string>();
        foreach (var key in specs.Keys)
            data.Add(key);
        return data;
    }

    protected async Task<(HttpResponseMessage Response, string Content)> PutRawAsync(string route, string payload, string mediaType = "application/json")
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, route)
        {
            Content = new StringContent(payload, Encoding.UTF8, mediaType)
        };

        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        return (response, content);
    }

    protected static Product CreateValidProduct(
        Guid? id = null,
        Guid? storeId = null,
        string? name = null,
        string? sku = null,
        decimal price = 79.99m,
        int stockQuantity = 15,
        decimal? weight = 0.9m,
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
            Name = name ?? $"UpdateRangeProduct-{token}",
            Sku = sku ?? $"URNG-{token}",
            Price = price,
            AddedIn = new DateOnly(2026, 5, 1),
            AddedAt = addedAt,
            FinishedAt = TimeSpan.FromHours(9),
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
        string? comment = "Update range review",
        TimeOnly? addedAt = null)
    {
        return new Review
        {
            ProductId = productId,
            CustomerId = customerId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
            AddedIn = new DateOnly(2026, 5, 1),
            AddedAt = addedAt,
            FinishedAt = TimeSpan.FromHours(4),
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

    protected async Task SeedProductsAsync(IEnumerable<Product> entities)
    {
        var list = entities.ToList();
        if (list.Count == 0)
            return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Products.AddRange(list);
        await db.SaveChangesAsync();
    }

    protected async Task SeedReviewsAsync(IEnumerable<Review> entities)
    {
        var list = entities.ToList();
        if (list.Count == 0)
            return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Reviews.AddRange(list);
        await db.SaveChangesAsync();
    }

    protected async Task CleanupProductsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entities = await db.Products.Where(x => idList.Contains(x.Id)).ToListAsync();
        if (entities.Count == 0)
            return;

        db.Products.RemoveRange(entities);
        await db.SaveChangesAsync();
    }

    protected async Task CleanupReviewsAsync(IEnumerable<(Guid ProductId, Guid CustomerId)> keys)
    {
        var keyList = keys.Distinct().ToList();
        if (keyList.Count == 0)
            return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var key in keyList)
        {
            var entity = await db.Reviews.FirstOrDefaultAsync(x => x.ProductId == key.ProductId && x.CustomerId == key.CustomerId);
            if (entity is not null)
                db.Reviews.Remove(entity);
        }

        await db.SaveChangesAsync();
    }
}
