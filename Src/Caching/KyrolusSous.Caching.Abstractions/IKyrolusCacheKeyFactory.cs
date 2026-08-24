namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Defines a standardized factory contract for generating consistent, partitioned cache key strings 
/// across tenants, regions, tags, and entry sets.
/// </summary>
public interface IKyrolusCacheKeyFactory
{
    /// <summary>
    /// Constructs a fully qualified cache key with optional region and tenant isolation prefixes.
    /// </summary>
    /// <param name="key">The raw base key (e.g. <c>"user:101"</c>).</param>
    /// <param name="region">Optional logical region name (e.g. <c>"identity"</c>).</param>
    /// <param name="tenantId">Optional tenant identifier (e.g. <c>"tenant_eg"</c>).</param>
    /// <returns>A formatted composite cache key (e.g. <c>"tenant_eg:identity:user:101"</c>).</returns>
    string BuildKey(string key, string? region = null, string? tenantId = null);

    /// <summary>
    /// Constructs a standardized Redis Set key used to index all cache keys associated with a specific tag.
    /// </summary>
    /// <param name="tag">The logical tag name (e.g. <c>"products"</c>).</param>
    /// <param name="region">Optional region name.</param>
    /// <param name="tenantId">Optional tenant identifier.</param>
    /// <returns>A formatted tag set key string (e.g. <c>"tag:tenant_eg:products"</c>).</returns>
    string BuildTagKey(string tag, string? region = null, string? tenantId = null);

    /// <summary>
    /// Constructs the tracking key used to store the reverse-index of tags assigned to a specific cache key.
    /// </summary>
    /// <param name="key">The base cache key.</param>
    /// <returns>A formatted entry tags tracking key string.</returns>
    string BuildEntryTagsKey(string key);
}
