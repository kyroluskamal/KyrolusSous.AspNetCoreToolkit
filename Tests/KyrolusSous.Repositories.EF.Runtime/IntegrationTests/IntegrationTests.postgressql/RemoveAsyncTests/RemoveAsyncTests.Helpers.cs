using System.Text;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.RemoveAsyncTests;

public partial class RemoveAsyncTests
{
    protected static TheoryData<string> CaseIdsFrom<TSpec>(IReadOnlyDictionary<string, TSpec> specs)
    {
        var data = new TheoryData<string>();
        foreach (var key in specs.Keys)
            data.Add(key);
        return data;
    }

    protected async Task<(HttpResponseMessage Response, string Content)> DeleteRawAsync(
        string route,
        string? payload = null,
        string mediaType = "application/json")
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, route);
        if (payload is not null)
            request.Content = new StringContent(payload, Encoding.UTF8, mediaType);

        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        return (response, content);
    }

    protected Task<(HttpResponseMessage Response, string Content)> DeleteSingleKeyAsync<TEntity>(
        Guid id,
        bool softDelete = false,
        bool useTryRoute = false)
    {
        var routeSuffix = useTryRoute ? "/try" : string.Empty;
        var route = $"/api/{typeof(TEntity).Name.ToLowerInvariant()}/{id}{routeSuffix}?softDelete={ToQueryBoolean(softDelete)}";
        return DeleteRawAsync(route);
    }

    protected Task<(HttpResponseMessage Response, string Content)> DeleteCompositeKeyAsync<TEntity>(
        object?[] keys,
        bool softDelete = false,
        bool useTryRoute = false)
    {
        var routePath = useTryRoute ? "try/by-id" : "by-id";
        var keysQuery = string.Join("&", keys.Select(x => $"keys={Uri.EscapeDataString(x?.ToString() ?? string.Empty)}"));
        var separator = string.IsNullOrWhiteSpace(keysQuery) ? string.Empty : $"{keysQuery}&";
        var route = $"/api/{typeof(TEntity).Name.ToLowerInvariant()}/{routePath}?{separator}softDelete={ToQueryBoolean(softDelete)}";
        return DeleteRawAsync(route);
    }

    protected Task<(HttpResponseMessage Response, string Content)> DeleteEntityRangeAsync<TEntity>(
        IEnumerable<TEntity> entities,
        bool softDelete = false)
    {
        var route = $"/api/{typeof(TEntity).Name.ToLowerInvariant()}/remove-range?softDelete={ToQueryBoolean(softDelete)}";
        var payload = JsonSerializer.Serialize(entities, JsonOptions);
        return DeleteRawAsync(route, payload);
    }

    protected static Product CreateValidProduct(
        Guid? id = null,
        Guid? storeId = null,
        string? name = null,
        string? sku = null,
        decimal price = 45m,
        int stockQuantity = 9,
        decimal? weight = 0.5m,
        int? count = 3)
    {
        var entityId = id ?? Guid.NewGuid();
        var token = entityId.ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        return new Product
        {
            Id = entityId,
            StoreId = storeId ?? DataSeeder.storeId,
            Name = name ?? $"RemoveProduct-{token}",
            Sku = sku ?? $"RM-{token}",
            Price = price,
            AddedIn = new DateOnly(2026, 2, 1),
            AddedAt = new TimeOnly(11, 15),
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
        Guid? productId = null,
        Guid? customerId = null,
        int rating = 4,
        string? comment = "Remove review")
    {
        return new Review
        {
            ProductId = productId ?? DataSeeder.productLaptopId,
            CustomerId = customerId ?? DataSeeder.customerJohnId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
            AddedIn = new DateOnly(2026, 2, 1),
            AddedAt = new TimeOnly(9, 30),
            FinishedAt = TimeSpan.FromHours(6),
            DiscontinuedAt = null,
            IsDeleted = false,
            DeletedAt = null
        };
    }

    protected async Task SeedProductAsync(Product product)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Products.Add(product);
        await db.SaveChangesAsync();
    }

    protected async Task SeedReviewAsync(Review review)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Reviews.Add(review);
        await db.SaveChangesAsync();
    }

    protected async Task SeedProductsAsync(IEnumerable<Product> products)
    {
        var entities = products.ToList();
        if (entities.Count == 0)
            return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Products.AddRange(entities);
        await db.SaveChangesAsync();
    }

    protected async Task SeedReviewsAsync(IEnumerable<Review> reviews)
    {
        var entities = reviews.ToList();
        if (entities.Count == 0)
            return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Reviews.AddRange(entities);
        await db.SaveChangesAsync();
    }

    protected async Task<bool> ProductExistsAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Products.IgnoreQueryFilters().AnyAsync(x => x.Id == id);
    }

    protected async Task<bool> ReviewExistsAsync(Guid productId, Guid customerId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Reviews.IgnoreQueryFilters().AnyAsync(x => x.ProductId == productId && x.CustomerId == customerId);
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
        var entity = await db.Reviews.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.ProductId == productId && x.CustomerId == customerId);
        if (entity is null)
            return;

        db.Reviews.Remove(entity);
        await db.SaveChangesAsync();
    }

    protected async Task CleanupProductsAsync(IEnumerable<Guid> ids)
    {
        var productIds = ids.Distinct().ToArray();
        if (productIds.Length == 0)
            return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entities = await db.Products.IgnoreQueryFilters()
            .Where(x => productIds.Contains(x.Id))
            .ToListAsync();

        if (entities.Count == 0)
            return;

        db.Products.RemoveRange(entities);
        await db.SaveChangesAsync();
    }

    protected async Task CleanupReviewsAsync(IEnumerable<(Guid ProductId, Guid CustomerId)> keys)
    {
        var keyList = keys.ToList();
        if (keyList.Count == 0)
            return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var key in keyList)
        {
            var entity = await db.Reviews.IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.ProductId == key.ProductId && x.CustomerId == key.CustomerId);
            if (entity is not null)
                db.Reviews.Remove(entity);
        }

        await db.SaveChangesAsync();
    }

    private static string ToQueryBoolean(bool value)
        => value ? "true" : "false";
}
