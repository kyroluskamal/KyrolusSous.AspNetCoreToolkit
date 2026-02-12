using System.Text;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.AddRangeAsyncTests;

public partial class AddRangeAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    protected static TheoryData<string> CaseIdsFrom<TSpec>(IReadOnlyDictionary<string, TSpec> specs)
    {
        var data = new TheoryData<string>();
        foreach (var key in specs.Keys)
            data.Add(key);
        return data;
    }

    protected async Task<(HttpResponseMessage Response, string Content)> PostEntityRangeAsync<TEntity>(IEnumerable<TEntity> payload)
        => await PostRawAsync(
            $"/api/{typeof(TEntity).Name.ToLowerInvariant()}/add-range",
            JsonSerializer.Serialize(payload, JsonOptions));

    protected async Task<(HttpResponseMessage Response, string Content)> PostRawAsync(string route, string payload, string mediaType = "application/json")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = new StringContent(payload, Encoding.UTF8, mediaType)
        };

        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        return (response, content);
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

    protected static Product CreateValidProduct(
        Guid? id = null,
        Guid? storeId = null,
        string? name = null,
        string? sku = null,
        decimal price = 49.99m,
        int stockQuantity = 5,
        decimal? weight = 0.5m,
        int? count = 2,
        TimeOnly? addedAt = null)
    {
        var entityId = id ?? Guid.NewGuid();
        var token = entityId.ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        return new Product
        {
            Id = entityId,
            StoreId = storeId ?? DataSeeder.storeId,
            Name = name ?? $"RangeProduct-{token}",
            Sku = sku ?? $"RNG-{token}",
            Price = price,
            AddedIn = new DateOnly(2026, 3, 1),
            AddedAt = addedAt,
            FinishedAt = TimeSpan.FromHours(6),
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
        string? comment = "Review from add-range",
        TimeOnly? addedAt = null)
    {
        return new Review
        {
            ProductId = productId,
            CustomerId = customerId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
            AddedIn = new DateOnly(2026, 3, 1),
            AddedAt = addedAt,
            FinishedAt = TimeSpan.FromHours(8),
            DiscontinuedAt = null,
            IsDeleted = false,
            DeletedAt = null
        };
    }
}

