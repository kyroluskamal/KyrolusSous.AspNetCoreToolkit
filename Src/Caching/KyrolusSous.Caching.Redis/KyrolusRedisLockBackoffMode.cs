namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Specifies the polling backoff strategy used when repeatedly attempting to acquire a contested distributed lock in Redis.
/// </summary>
public enum KyrolusRedisLockBackoffMode
{
    /// <summary>
    /// Waits a fixed constant duration (e.g. exactly 50ms) between retry attempts.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// Best for low-contention environments or short-lived operations where immediate lock acquisition is desired.
    /// </remarks>
    Fixed = 0,

    /// <summary>
    /// Progressively increases the delay between retry attempts (e.g. 50ms, 100ms, 200ms, 400ms...) up to a maximum limit.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (High-Contention Spikes):</b>
    /// During heavy flash sales when hundreds of threads compete for the same inventory lock, 
    /// exponential backoff prevents a "thundering retry storm" against the Redis instance.
    /// </remarks>
    Exponential = 1
}
