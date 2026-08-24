namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Specifies the target scope and mechanism of a cluster-wide cache invalidation event.
/// </summary>
public enum KyrolusCacheInvalidationKind
{
    /// <summary>
    /// Invalidate a single explicit cache key.
    /// </summary>
    Key = 1,

    /// <summary>
    /// Invalidate multiple explicit cache keys in batch.
    /// </summary>
    Keys = 2,

    /// <summary>
    /// Invalidate all cache keys associated with a specific logical tag.
    /// </summary>
    Tag = 3,

    /// <summary>
    /// Invalidate all cache keys matching a glob wildcard pattern (e.g., <c>"user:*"</c>).
    /// </summary>
    Pattern = 4
}
