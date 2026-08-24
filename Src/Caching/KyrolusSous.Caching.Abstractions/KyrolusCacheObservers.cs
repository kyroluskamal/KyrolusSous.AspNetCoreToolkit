namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Specifies the observational outcome of a cache operation.
/// </summary>
public enum KyrolusCacheObservation
{
    /// <summary>
    /// Item was found in cache.
    /// </summary>
    Hit,

    /// <summary>
    /// Item was missing or expired in cache.
    /// </summary>
    Miss,

    /// <summary>
    /// Item was successfully stored.
    /// </summary>
    Set,

    /// <summary>
    /// Item was removed from cache.
    /// </summary>
    Remove,

    /// <summary>
    /// Item existence was verified.
    /// </summary>
    Exists,

    /// <summary>
    /// An exception occurred during the cache operation.
    /// </summary>
    Error,

    /// <summary>
    /// A distributed lock was successfully acquired.
    /// </summary>
    LockAcquired,

    /// <summary>
    /// A distributed lock acquisition failed or timed out.
    /// </summary>
    LockFailed
}

/// <summary>
/// Encapsulates diagnostic context information passed to <see cref="IKyrolusCacheObserver"/> instances.
/// </summary>
/// <param name="Key">The cache key involved.</param>
/// <param name="Operation">The executed cache operation.</param>
/// <param name="Observation">The observational outcome (Hit, Miss, Error, etc.).</param>
/// <param name="ValueType">The C# type of the cached object.</param>
/// <param name="Duration">The execution duration of the operation.</param>
/// <param name="Region">Optional cache region.</param>
/// <param name="TenantId">Optional tenant ID.</param>
/// <param name="Exception">The exception thrown, if any.</param>
public sealed record KyrolusCacheObserverContext(
    string Key,
    KyrolusCacheOperation Operation,
    KyrolusCacheObservation Observation,
    Type? ValueType,
    TimeSpan? Duration,
    string? Region,
    string? TenantId,
    Exception? Exception);

/// <summary>
/// Defines an observer contract for intercepting and logging cache events (hits, misses, errors, locks) for custom diagnostics or auditing.
/// </summary>
public interface IKyrolusCacheObserver
{
    /// <summary>
    /// Invoked asynchronously when a cache operation is completed.
    /// </summary>
    /// <param name="context">The contextual observation data.</param>
    Task OnObservationAsync(KyrolusCacheObserverContext context);
}

/// <summary>
/// No-op implementation of <see cref="IKyrolusCacheObserver"/>.
/// </summary>
public sealed class KyrolusNullCacheObserver : IKyrolusCacheObserver
{
    /// <summary>
    /// Gets the singleton instance of <see cref="KyrolusNullCacheObserver"/>.
    /// </summary>
    public static IKyrolusCacheObserver Instance { get; } = new KyrolusNullCacheObserver();

    /// <inheritdoc />
    public Task OnObservationAsync(KyrolusCacheObserverContext context) => Task.CompletedTask;
}
