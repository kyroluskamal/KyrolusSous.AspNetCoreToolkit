namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Specifies how wildcard pattern-based key removals (<c>RemoveKeysByPatternAsync</c>) are executed in Redis.
/// </summary>
public enum KyrolusRedisPatternRemovalStrategy
{
    /// <summary>
    /// Tracks all active cache keys in a local Redis set index, enabling instant pattern matching without scanning Redis.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (Fast &amp; Safe Pattern Evictions):</b>
    /// When you need fast pattern deletions (e.g. <c>"user:42:*"</c>) without freezing Redis or running slow cluster scans.
    /// </remarks>
    KeyIndex = 1,

    /// <summary>
    /// Performs non-blocking cursor-based SCAN iterations across all Redis server master nodes.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// When key indexing is disabled to save memory, and pattern removals are performed infrequently (e.g. during admin deployments).
    /// </remarks>
    ServerScan = 2,

    /// <summary>
    /// Disables pattern-based key removal operations entirely (No-Op).
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (Large High-Throughput Production Clusters):</b>
    /// Highly recommended for extreme-scale production clusters where SCAN operations are strictly prohibited to protect Redis SLAs.
    /// </remarks>
    Disabled = 3
}
