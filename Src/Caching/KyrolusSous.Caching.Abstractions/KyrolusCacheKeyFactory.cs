namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Standard implementation of <see cref="IKyrolusCacheKeyFactory"/> that formats hierarchical Redis keys 
/// using colon-separated segments: <c>"[prefix]:[region]:[tenantId]:[key]"</c>.
/// </summary>
/// <param name="prefix">Optional global application prefix (e.g. <c>"myapp"</c>).</param>
public sealed class KyrolusCacheKeyFactory(string? prefix = null) : IKyrolusCacheKeyFactory
{
    private readonly string? prefix = string.IsNullOrWhiteSpace(prefix) ? null : prefix!.Trim();

    /// <summary>
    /// Constructs a fully qualified hierarchical cache key string.
    /// </summary>
    /// <param name="key">The raw base key (e.g. <c>"user:101"</c>).</param>
    /// <param name="region">Optional logical region name.</param>
    /// <param name="tenantId">Optional tenant identifier.</param>
    /// <returns>A formatted composite cache key (e.g. <c>"myapp:catalog:tenant1:user:101"</c>).</returns>
    public string BuildKey(string key, string? region = null, string? tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

        var parts = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(prefix)) parts.Add(prefix!);
        if (!string.IsNullOrWhiteSpace(region)) parts.Add(region!);
        if (!string.IsNullOrWhiteSpace(tenantId)) parts.Add(tenantId!);
        parts.Add(key);
        return string.Join(':', parts);
    }

    /// <summary>
    /// Constructs the Redis Set key for a logical tag index.
    /// </summary>
    /// <param name="tag">The logical tag name.</param>
    /// <param name="region">Optional region name.</param>
    /// <param name="tenantId">Optional tenant identifier.</param>
    /// <returns>A formatted tag key (e.g. <c>"myapp:tag:products"</c>).</returns>
    public string BuildTagKey(string tag, string? region = null, string? tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag cannot be null or whitespace.", nameof(tag));

        return BuildKey($"tag:{tag}", region, tenantId);
    }

    /// <summary>
    /// Constructs the reverse tracking key containing the set of tags associated with a specific cache key.
    /// </summary>
    /// <param name="key">The target cache key.</param>
    /// <returns>A formatted entry tags key (e.g. <c>"myapp:tags:user:101"</c>).</returns>
    public string BuildEntryTagsKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

        return BuildKey($"tags:{key}");
    }
}
