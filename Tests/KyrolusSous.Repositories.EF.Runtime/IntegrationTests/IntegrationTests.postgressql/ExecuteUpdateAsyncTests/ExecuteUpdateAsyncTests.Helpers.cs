using System.Linq.Expressions;
using System.Text;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.ExecuteUpdateAsyncTests;

public partial class ExecuteUpdateAsyncTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    protected sealed record PropertyUpdateRequest(string Property, object? Value);
    private sealed record ExecuteUpdateRequest(QueryRequest? Request, List<PropertyUpdateRequest>? Updates);

    protected static TheoryData<string> CaseIdsFrom<TSpec>(IReadOnlyDictionary<string, TSpec> specs)
    {
        var data = new TheoryData<string>();
        foreach (var key in specs.Keys)
            data.Add(key);
        return data;
    }

    protected async Task<(HttpResponseMessage Response, string Content)> PostRawAsync(
        string route,
        string payload,
        string mediaType = "application/json")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = new StringContent(payload, Encoding.UTF8, mediaType)
        };

        var response = await _client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        return (response, content);
    }

    protected async Task<(HttpResponseMessage Response, int? Affected, string Content)> PostExecuteUpdateAsync<TEntity>(
        QueryRequest? request,
        List<PropertyUpdateRequest>? updates)
    {
        var route = $"/api/{typeof(TEntity).Name.ToLowerInvariant()}/execute-update";
        var payload = JsonSerializer.Serialize(new ExecuteUpdateRequest(request, updates), JsonOptions);
        var (response, content) = await PostRawAsync(route, payload);
        var affected = response.IsSuccessStatusCode
            ? (int?)JsonSerializer.Deserialize<int>(content, JsonOptions)
            : null;
        return (response, affected, content);
    }

    protected static Product CreateValidProduct(
        Guid? id = null,
        Guid? storeId = null,
        string? name = null,
        string? sku = null,
        decimal price = 99.5m,
        int stockQuantity = 11,
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
            Name = name ?? $"ExecuteUpdateProduct-{token}",
            Sku = sku ?? $"EXU-{token}",
            Price = price,
            AddedIn = new DateOnly(2026, 12, 25),
            AddedAt = addedAt,
            FinishedAt = TimeSpan.FromHours(7),
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
        string? comment = "Execute update review",
        TimeOnly? addedAt = null)
    {
        return new Review
        {
            ProductId = productId,
            CustomerId = customerId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
            AddedIn = new DateOnly(2026, 12, 25),
            AddedAt = addedAt,
            FinishedAt = TimeSpan.FromHours(3),
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

    protected async Task<Product?> FindProductAsync(Guid id, bool ignoreFilters = false)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var query = ignoreFilters ? db.Products.IgnoreQueryFilters() : db.Products.AsQueryable();
        return await query.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
    }

    protected async Task<Review?> FindReviewAsync(Guid productId, Guid customerId, bool ignoreFilters = false)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var query = ignoreFilters ? db.Reviews.IgnoreQueryFilters() : db.Reviews.AsQueryable();
        return await query.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == productId && x.CustomerId == customerId);
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

    protected async Task CleanupProductsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.Distinct().ToArray();
        if (idList.Length == 0)
            return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entities = await db.Products.IgnoreQueryFilters()
            .Where(x => idList.Contains(x.Id))
            .ToListAsync();
        if (entities.Count == 0)
            return;

        db.Products.RemoveRange(entities);
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

    protected async Task CleanupReviewsAsync(IEnumerable<(Guid ProductId, Guid CustomerId)> keys)
    {
        var keyList = keys.Distinct().ToArray();
        if (keyList.Length == 0)
            return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var key in keyList)
        {
            var entity = await db.Reviews.IgnoreQueryFilters()
                .SingleOrDefaultAsync(x => x.ProductId == key.ProductId && x.CustomerId == key.CustomerId);
            if (entity is not null)
                db.Reviews.Remove(entity);
        }

        await db.SaveChangesAsync();
    }
}
