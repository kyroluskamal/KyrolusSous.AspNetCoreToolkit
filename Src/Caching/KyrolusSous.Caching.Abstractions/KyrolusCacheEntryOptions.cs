namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Configures expiration policies, protection against traffic surges (Jitter), negative result caching, 
/// tag-based bulk eviction, and multi-tenant partitioning for individual cache items.
/// </summary>
public sealed class KyrolusCacheEntryOptions
{
    /// <summary>
    /// Gets or sets an absolute expiration duration starting from the moment of storage.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (Currency Exchange Rates &amp; Daily Reports):</b>
    /// When you cache foreign exchange rates (e.g., USD to EGP) updated by the central bank every morning, 
    /// you set <c>AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4)</c>. After exactly 4 hours, 
    /// the cached rate is removed, regardless of how many users visited the page.
    /// </remarks>
    public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }

    /// <summary>
    /// Gets or sets an inactivity timeout that resets every time the item is accessed.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (User Shopping Carts):</b>
    /// If you set <c>SlidingExpiration = TimeSpan.FromMinutes(20)</c>, the customer's cart stays cached 
    /// as long as they interact with the store every 15 minutes. Once the user closes the app or goes idle for 
    /// a full 20 minutes, the cart is evicted to free up RAM.
    /// </remarks>
    public TimeSpan? SlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets a random time variance (Jitter) added to the expiration duration 
    /// to protect the database against <b>Cache Avalanche (Cache Stampede)</b>.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (Midnight Catalog Refresh):</b>
    /// Suppose your catalog service caches 500,000 products at midnight with a 2-hour TTL. 
    /// Without Jitter, all 500,000 keys expire at the exact same millisecond (2:00:00 AM), 
    /// causing hundreds of thousands of requests to hit the database simultaneously and crashing the server.
    /// By setting <c>Jitter = TimeSpan.FromMinutes(10)</c>, expiration times are randomly scattered 
    /// between 1h 50m and 2h 10m, smoothing database queries into a gentle stream.
    /// </remarks>
    public TimeSpan? Jitter { get; set; }

    /// <summary>
    /// Gets or sets a short expiration duration used when caching negative results (<c>null</c> or Not Found responses) 
    /// to defend the database against <b>Cache Penetration Attacks</b>.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (Bot Queries on Non-Existent IDs):</b>
    /// If an attacker or search scraper sends 10,000 requests per second looking for fake user IDs (like <c>/users/99999999</c>), 
    /// the cache would ordinarily report a "Miss" every time, forcing the system to query SQL database 10,000 times per second.
    /// By caching the negative <c>null</c> result for 30 seconds (<c>NegativeExpirationRelativeToNow = TimeSpan.FromSeconds(30)</c>), 
    /// subsequent requests for that fake ID return "Not Found" instantly from Redis without touching the SQL database.
    /// </remarks>
    public TimeSpan? NegativeExpirationRelativeToNow { get; set; }

    /// <summary>
    /// Gets or sets a list of logical category tags associated with this cache entry for bulk invalidation.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (Updating a Product Category):</b>
    /// When you cache 1,000 different laptops, you tag each one with <c>Tags = ["laptops", "electronics"]</c>.
    /// When an administrator updates category settings or discounts for all laptops, they simply call 
    /// <c>cache.RemoveByTagAsync("laptops")</c> to evict all 1,000 laptops in a single call, instead of searching 
    /// and deleting 1,000 individual keys.
    /// </remarks>
    public IReadOnlyCollection<string>? Tags { get; set; }

    /// <summary>
    /// Gets or sets the logical cache region or domain partition name (e.g. <c>"catalog"</c>, <c>"identity"</c>, or <c>"billing"</c>).
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (Microservice Domain Separation):</b>
    /// Allows separating different subsystems sharing the same Redis instance (e.g. Identity vs Inventory) 
    /// so that keys do not collide or overwrite one another.
    /// </remarks>
    public string? Region { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier for multi-tenant data isolation.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (SaaS Multi-Tenancy):</b>
    /// In a SaaS platform where Company A and Company B both have an employee with <c>Id = 1</c>, 
    /// setting <c>TenantId = "company_a"</c> ensures Company A never reads Company B's cached salary or records.
    /// </remarks>
    public string? TenantId { get; set; }
}
