namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Represents a distributed lock handle acquired from an <see cref="IDistributedLockProvider"/>.
/// Disposing this handle releases the distributed lock.
/// </summary>
public interface IDistributedLockHandle : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the unique lock key.
    /// </summary>
    string LockKey { get; }

    /// <summary>
    /// Gets the unique owner token for this lock acquisition.
    /// </summary>
    string LockToken { get; }

    /// <summary>
    /// Gets a value indicating whether the lock was successfully acquired.
    /// </summary>
    bool IsAcquired { get; }
}

/// <summary>
/// Provides standalone distributed locking capabilities across distributed nodes.
/// </summary>
public interface IDistributedLockProvider
{
    /// <summary>
    /// Attempts to acquire a distributed lock on the specified resource key.
    /// Returns a lock handle if acquired, or <c>null</c> if the lock could not be acquired within the timeout.
    /// </summary>
    /// <param name="key">The unique resource identifier to lock.</param>
    /// <param name="timeout">The maximum duration to wait while attempting to acquire the lock.</param>
    /// <param name="lockExpiry">The lock auto-expiration duration (TTL) to prevent deadlocks if the holder crashes. If null, a default TTL is used.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A lock handle if acquired; otherwise, null.</returns>
    Task<IDistributedLockHandle?> TryAcquireLockAsync(
        string key,
        TimeSpan timeout,
        TimeSpan? lockExpiry = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a distributed lock on the specified resource key, or throws a <see cref="TimeoutException"/> if the lock cannot be acquired within the timeout.
    /// </summary>
    /// <param name="key">The unique resource identifier to lock.</param>
    /// <param name="timeout">The maximum duration to wait while attempting to acquire the lock.</param>
    /// <param name="lockExpiry">The lock auto-expiration duration (TTL) to prevent deadlocks if the holder crashes. If null, a default TTL is used.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A lock handle.</returns>
    /// <exception cref="TimeoutException">Thrown when the lock cannot be acquired within the specified timeout.</exception>
    Task<IDistributedLockHandle> AcquireLockAsync(
        string key,
        TimeSpan timeout,
        TimeSpan? lockExpiry = null,
        CancellationToken cancellationToken = default);
}
