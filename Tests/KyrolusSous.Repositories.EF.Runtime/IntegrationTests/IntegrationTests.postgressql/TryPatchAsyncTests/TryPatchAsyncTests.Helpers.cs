using System.Text;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryPatchAsyncTests;

public partial class TryPatchAsyncTests
{
    protected static TheoryData<string> CaseIdsFrom<TSpec>(IReadOnlyDictionary<string, TSpec> specs)
    {
        var data = new TheoryData<string>();
        foreach (var key in specs.Keys)
            data.Add(key);
        return data;
    }

    protected async Task<(HttpResponseMessage Response, string Content)> PatchRawAsync(
        string route,
        string? payload = null,
        string mediaType = "application/json")
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, route);
        if (payload is not null)
            request.Content = new StringContent(payload, Encoding.UTF8, mediaType);

        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        return (response, content);
    }

    protected static Product CreateValidProduct(
        Guid? id = null,
        Guid? storeId = null,
        string? name = null,
        string? sku = null,
        decimal price = 79.5m,
        int stockQuantity = 7,
        decimal? weight = 1.1m,
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
            Name = name ?? $"TryPatchProduct-{token}",
            Sku = sku ?? $"TPH-{token}",
            Price = price,
            AddedIn = new DateOnly(2026, 7, 1),
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
        string? comment = "TryPatch review",
        TimeOnly? addedAt = null)
    {
        return new Review
        {
            ProductId = productId,
            CustomerId = customerId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
            AddedIn = new DateOnly(2026, 7, 1),
            AddedAt = addedAt,
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
