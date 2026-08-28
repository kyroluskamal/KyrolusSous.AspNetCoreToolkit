namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Represents an active distributed lock lease. Disposing this handle releases the lock in Redis.
/// </summary>
public interface IKyrolusDistributedLockHandle : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the unique resource key being locked (e.g. <c>"lock:wallet:user_42"</c>).
    /// </summary>
    string LockKey { get; }

    /// <summary>
    /// Gets the unique random token assigned to this lock owner to ensure only the creator can release it.
    /// </summary>
    string LockToken { get; }

    /// <summary>
    /// Gets a value indicating whether the lock was successfully acquired within the timeout window.
    /// </summary>
    bool IsAcquired { get; }
}

/// <summary>
/// Provides cross-server distributed locking capabilities to guarantee mutual exclusion across multiple instances of an application.
/// </summary>
public interface IKyrolusDistributedLockProvider
{
    /// <summary>
    /// Attempts to acquire a distributed lock on the specified resource key without throwing an exception upon failure.
    /// </summary>
    Task<IKyrolusDistributedLockHandle?> TryAcquireLockAsync(
        string key,
        TimeSpan timeout,
        TimeSpan? lockExpiry = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a distributed lock on the specified resource key, or throws a <see cref="TimeoutException"/> if the lock is held by another instance beyond the allowed timeout.
    /// </summary>
    Task<IKyrolusDistributedLockHandle> AcquireLockAsync(
        string key,
        TimeSpan timeout,
        TimeSpan? lockExpiry = null,
        CancellationToken cancellationToken = default);
}
