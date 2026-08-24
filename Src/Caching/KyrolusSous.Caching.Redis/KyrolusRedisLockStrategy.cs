namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Specifies the underlying locking and release implementation used by the Redis distributed lock provider.
/// </summary>
public enum KyrolusRedisLockStrategy
{
    /// <summary>
    /// Uses an atomic Redis Lua script to verify lock ownership token before releasing (Standard Redlock algorithm).
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (Recommended for Production):</b>
    /// Prevents the dangerous race condition where a slow request's lock expires, Server 2 acquires the lock, 
    /// and then Server 1 finishes and accidentally deletes Server 2's lock! The Lua script guarantees only the true owner can release it.
    /// </remarks>
    Lua = 1,

    /// <summary>
    /// Simple non-atomic Redis key check and deletion.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// Used only when interacting with legacy Redis proxies or cluster configurations that restrict Lua script execution.
    /// </remarks>
    Simple = 2,

    /// <summary>
    /// Disables distributed locking entirely (No-Op).
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// In single-instance applications or read-heavy workloads where concurrency contention does not exist.
    /// </remarks>
    Disabled = 3
}
