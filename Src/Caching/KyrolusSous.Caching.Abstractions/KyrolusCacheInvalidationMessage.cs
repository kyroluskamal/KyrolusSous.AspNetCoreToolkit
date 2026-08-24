namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Represents the serializable message payload published over the invalidation bus (<see cref="IKyrolusCacheInvalidationBus"/>) 
/// to notify all cluster nodes to evict stale cache entries.
/// </summary>
/// <param name="Kind">The invalidation mechanism (Key, Keys, Tag, or Pattern).</param>
/// <param name="Values">The collection of keys, tags, or patterns targeted for eviction.</param>
public sealed record KyrolusCacheInvalidationMessage(
    KyrolusCacheInvalidationKind Kind,
    IReadOnlyCollection<string> Values);
