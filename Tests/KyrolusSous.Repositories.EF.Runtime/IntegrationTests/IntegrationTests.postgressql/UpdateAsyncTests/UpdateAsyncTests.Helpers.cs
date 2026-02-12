using System.Text;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.UpdateAsyncTests;

public partial class UpdateAsyncTests
{
    protected static TheoryData<string> CaseIdsFrom<TSpec>(IReadOnlyDictionary<string, TSpec> specs)
    {
        var data = new TheoryData<string>();
        foreach (var key in specs.Keys)
            data.Add(key);
        return data;
    }

    protected async Task<(HttpResponseMessage Response, string Content)> PutEntityAsync<TEntity>(object payload, string? routeId = null)
    {
        var basePath = $"/api/{typeof(TEntity).Name.ToLowerInvariant()}";
        var route = string.IsNullOrWhiteSpace(routeId) ? basePath : $"{basePath}/{routeId}";
        return await PutRawAsync(route, JsonSerializer.Serialize(payload, JsonOptions));
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
        decimal price = 59.99m,
        int stockQuantity = 10,
        decimal? weight = 0.8m,
        int? count = 4,
        TimeOnly? addedAt = null)
    {
        var entityId = id ?? Guid.NewGuid();
        var token = entityId.ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        return new Product
        {
            Id = entityId,
            StoreId = storeId ?? DataSeeder.storeId,
            Name = name ?? $"UpdateProduct-{token}",
            Sku = sku ?? $"UPD-{token}",
            Price = price,
            AddedIn = new DateOnly(2026, 4, 1),
            AddedAt = addedAt,
            FinishedAt = TimeSpan.FromHours(12),
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
        string? comment = "Update review",
        TimeOnly? addedAt = null)
    {
        return new Review
        {
            ProductId = productId,
            CustomerId = customerId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
            AddedIn = new DateOnly(2026, 4, 1),
            AddedAt = addedAt,
            FinishedAt = TimeSpan.FromHours(6),
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

    protected async Task CleanupProductAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entity = await db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
            return;

        db.Products.Remove(entity);
        await db.SaveChangesAsync();
    }

    protected async Task CleanupReviewAsync(Guid productId, Guid customerId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entity = await db.Reviews.FirstOrDefaultAsync(x => x.ProductId == productId && x.CustomerId == customerId);
        if (entity is null)
            return;

        db.Reviews.Remove(entity);
        await db.SaveChangesAsync();
    }
}

