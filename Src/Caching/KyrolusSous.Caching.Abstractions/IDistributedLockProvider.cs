namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Represents an active distributed lock lease. Disposing this handle releases the lock in Redis.
/// </summary>
public interface IDistributedLockHandle : IAsyncDisposable, IDisposable
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
/// <remarks>
/// <b>Real-World Use Cases:</b>
/// <list type="bullet">
///   <item><description><b>Financial &amp; Wallet Deductions:</b> Preventing double-spending when a user clicks "Pay" twice rapidly or multiple API requests hit different server nodes simultaneously.</description></item>
///   <item><description><b>Background Scheduled Cron Jobs:</b> Ensuring that a daily billing job or report generator executes on <b>only one single server instance</b> in a 10-node Kubernetes cluster.</description></item>
///   <item><description><b>Limited Flash Sale Inventory:</b> Preventing overselling when the last available seat on a flight is booked by concurrent users.</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Acquire lock, execute critical task, and automatically release upon exiting using block
/// await using var lockHandle = await lockProvider.AcquireLockAsync(
///     $"lock:invoice:{invoiceId}",
///     timeout: TimeSpan.FromSeconds(3),
///     lockExpiry: TimeSpan.FromSeconds(30),
///     cancellationToken);
/// 
/// await ProcessInvoicePaymentAsync(invoiceId);
/// </code>
/// </example>
public interface IDistributedLockProvider
{
    /// <summary>
    /// Attempts to acquire a distributed lock on the specified resource key without throwing an exception upon failure.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (Background Tasks / Best Effort):</b>
    /// When executing an optional background sync task, if another server is already running it, you simply skip execution:
    /// <c>var handle = await lockProvider.TryAcquireLockAsync("sync:inventory", TimeSpan.Zero); if (handle is null) return;</c>
    /// </remarks>
    /// <param name="key">The resource key to lock.</param>
    /// <param name="timeout">The maximum duration to wait while trying to acquire the lock.</param>
    /// <param name="lockExpiry">The automatic lock expiration duration (TTL) to prevent deadlocks if the instance crashes. If <c>null</c>, uses default (10s).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A valid <see cref="IDistributedLockHandle"/> if acquired; otherwise, <c>null</c>.</returns>
    Task<IDistributedLockHandle?> TryAcquireLockAsync(
        string key,
        TimeSpan timeout,
        TimeSpan? lockExpiry = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a distributed lock on the specified resource key, or throws a <see cref="TimeoutException"/> if the lock is held by another instance beyond the allowed timeout.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (Mandatory Critical Transactions):</b>
    /// When processing an order payment where mutual exclusion is mandatory, waiting up to 5 seconds before failing:
    /// <c>await using var handle = await lockProvider.AcquireLockAsync($"lock:order:{orderId}", TimeSpan.FromSeconds(5));</c>
    /// </remarks>
    /// <param name="key">The resource key to lock.</param>
    /// <param name="timeout">The maximum duration to wait for the lock.</param>
    /// <param name="lockExpiry">The lock auto-expiration TTL duration. If <c>null</c>, uses default (10s).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="IDistributedLockHandle"/> that must be disposed to release the lock.</returns>
    /// <exception cref="TimeoutException">Thrown when the lock could not be acquired within the specified timeout duration.</exception>
    Task<IDistributedLockHandle> AcquireLockAsync(
        string key,
        TimeSpan timeout,
        TimeSpan? lockExpiry = null,
        CancellationToken cancellationToken = default);
}
