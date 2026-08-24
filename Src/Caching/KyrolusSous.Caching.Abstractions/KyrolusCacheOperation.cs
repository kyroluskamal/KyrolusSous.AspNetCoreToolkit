namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Specifies the type of operation executed against the cache provider.
/// </summary>
public enum KyrolusCacheOperation
{
    /// <summary>
    /// Single key read (GetAsync).
    /// </summary>
    Get,

    /// <summary>
    /// Batch keys read (GetManyAsync).
    /// </summary>
    GetMany,

    /// <summary>
    /// Single key write (SetAsync).
    /// </summary>
    Set,

    /// <summary>
    /// Batch keys write (SetManyAsync).
    /// </summary>
    SetMany,

    /// <summary>
    /// Single key removal (RemoveAsync).
    /// </summary>
    Remove,

    /// <summary>
    /// Batch keys removal (RemoveManyAsync).
    /// </summary>
    RemoveMany,

    /// <summary>
    /// Tag-based group invalidation (RemoveByTagAsync).
    /// </summary>
    RemoveByTag,

    /// <summary>
    /// Pattern-based wildcard invalidation (RemoveKeysByPatternAsync).
    /// </summary>
    RemoveByPattern,

    /// <summary>
    /// Key existence check (ExistsAsync).
    /// </summary>
    Exists,

    /// <summary>
    /// Cache-Aside atomic retrieval or factory execution (GetOrCreateAsync).
    /// </summary>
    GetOrCreate
}
