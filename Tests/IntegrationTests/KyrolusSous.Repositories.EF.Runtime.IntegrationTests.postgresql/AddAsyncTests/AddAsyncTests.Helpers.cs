using System.Text;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.AddAsyncTests;

public partial class AddAsyncTests
{
    protected static TheoryData<string> CaseIdsFrom<TSpec>(IReadOnlyDictionary<string, TSpec> specs)
    {
        var data = new TheoryData<string>();
        foreach (var key in specs.Keys)
            data.Add(key);
        return data;
    }

    protected async Task<(HttpResponseMessage Response, string Content)> PostEntityAsync<TEntity>(object payload)
        => await PostRawAsync($"/api/{typeof(TEntity).Name.ToLowerInvariant()}", JsonSerializer.Serialize(payload, JsonOptions));

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
#pragma warning disable S107

    protected static Product CreateValidProduct(
        Guid? id = null,
        Guid? storeId = null,
        string? name = null,
        string? sku = null,
        decimal price = 79.99m,
        int stockQuantity = 12,
        decimal? weight = 0.75m,
        int? count = 3,
        TimeOnly? addedAt = null)
    {
        var entityId = id ?? Guid.NewGuid();
        var token = entityId.ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        return new Product
        {
            Id = entityId,
            StoreId = storeId ?? DataSeeder.storeId,
            Name = name ?? $"Product-{token}",
            Sku = sku ?? $"SKU-{token}",
            Price = price,
            AddedIn = new DateOnly(2026, 1, 15),
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
#pragma warning restore S107

    protected static Review CreateValidReview(
        Guid? productId = null,
        Guid? customerId = null,
        int rating = 4,
        string? comment = "Solid experience.",
        TimeOnly? addedAt = null)
    {
        return new Review
        {
            ProductId = productId ?? DataSeeder.productLaptopId,
            CustomerId = customerId ?? DataSeeder.customerJohnId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
            AddedIn = new DateOnly(2026, 2, 1),
            AddedAt = addedAt,
            FinishedAt = TimeSpan.FromHours(10),
            DiscontinuedAt = null,
            IsDeleted = false,
            DeletedAt = null
        };
    }
}
