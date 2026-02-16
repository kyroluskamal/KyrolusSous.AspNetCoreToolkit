using System.Text;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.TryRemoveAsyncTests;

public partial class TryRemoveAsyncTests
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

    protected Task<(HttpResponseMessage Response, string Content)> DeleteSingleTryAsync<TEntity>(
        Guid id,
        bool softDelete = false)
    {
        var route = $"/api/{typeof(TEntity).Name.ToLowerInvariant()}/{id}/try?softDelete={ToQueryBoolean(softDelete)}";
        return DeleteRawAsync(route);
    }

    protected Task<(HttpResponseMessage Response, string Content)> DeleteCompositeTryAsync<TEntity>(
        object?[] keys,
        bool softDelete = false)
    {
        var keysQuery = string.Join("&", keys.Select(x => $"keys={Uri.EscapeDataString(x?.ToString() ?? string.Empty)}"));
        var separator = string.IsNullOrWhiteSpace(keysQuery) ? string.Empty : $"{keysQuery}&";
        var route = $"/api/{typeof(TEntity).Name.ToLowerInvariant()}/try/by-id?{separator}softDelete={ToQueryBoolean(softDelete)}";
        return DeleteRawAsync(route);
    }

    protected static Product CreateValidProduct(
        Guid? id = null,
        Guid? storeId = null,
        string? name = null,
        string? sku = null,
        decimal price = 77.25m,
        int stockQuantity = 5,
        decimal? weight = 0.8m,
        int? count = 4)
    {
        var entityId = id ?? Guid.NewGuid();
        var token = entityId.ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        return new Product
        {
            Id = entityId,
            StoreId = storeId ?? DataSeeder.storeId,
            Name = name ?? $"TryRemoveProduct-{token}",
            Sku = sku ?? $"TRM-{token}",
            Price = price,
            AddedIn = new DateOnly(2026, 2, 20),
            AddedAt = new TimeOnly(9, 0),
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
        string? comment = "TryRemove review")
    {
        return new Review
        {
            ProductId = productId ?? DataSeeder.productLaptopId,
            CustomerId = customerId ?? DataSeeder.customerJohnId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
            AddedIn = new DateOnly(2026, 2, 20),
            AddedAt = new TimeOnly(10, 15),
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

    private static string ToQueryBoolean(bool value)
        => value ? "true" : "false";
}
