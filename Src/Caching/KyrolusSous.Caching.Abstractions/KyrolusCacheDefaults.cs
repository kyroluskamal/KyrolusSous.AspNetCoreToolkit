namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Provides centralized default values and operational constants for the caching system, 
/// designed to protect memory, prevent deadlocks, and ensure high performance out-of-the-box.
/// </summary>
public static class KyrolusCacheDefaults
{
    /// <summary>
    /// Gets the default Time-To-Live (30 minutes) applied when a developer caches an item without specifying an expiration.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// If a developer writes <c>cache.SetAsync("product:10", product)</c> and forgets to set a duration, 
    /// this default prevents the item from staying in Redis forever, ensuring old prices are cleared 
    /// and memory is automatically reclaimed after 30 minutes.
    /// </remarks>
    public static TimeSpan DefaultTtl { get; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets the default sliding window duration (5 minutes) for cache entries that renew on access.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (User Sessions &amp; Shopping Carts):</b>
    /// As long as the customer browses the website and navigates pages every couple of minutes, 
    /// their active cart stays in memory. If they become inactive for 5 continuous minutes, the cache expires 
    /// to free up RAM.
    /// </remarks>
    public static TimeSpan DefaultSlidingTtl { get; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the default maximum lease duration (10 seconds) for distributed locks.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (Crash Protection / Deadlock Prevention):</b>
    /// When Server 1 acquires a lock to process a payment and suddenly suffers a hardware failure or power outage, 
    /// Redis automatically releases this lock after 10 seconds so that other servers are not permanently blocked.
    /// </remarks>
    public static TimeSpan DefaultLockTtl { get; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the maximum duration (2 seconds) a server will wait while polling to acquire a busy lock before giving up.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// When Server 2 wants to update an account balance that is currently locked by Server 1, 
    /// instead of failing immediately with an error, it waits up to 2 seconds for Server 1 to finish its quick task.
    /// </remarks>
    public static TimeSpan DefaultLockWait { get; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets the sleep interval (50 milliseconds) between polling attempts when trying to acquire a busy lock.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (CPU &amp; Network Throttling):</b>
    /// While waiting for a lock, the server pauses for 50ms before checking Redis again, 
    /// preventing a tight loop that would otherwise spike CPU to 100% and flood the network with Redis calls.
    /// </remarks>
    public static TimeSpan DefaultLockRetryDelay { get; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Gets the minimum payload size threshold in bytes (1024 bytes = 1 KB) required before triggering compression.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (Small Payloads &amp; OTPs):</b>
    /// Storing a 6-digit SMS verification code like <c>"581902"</c> takes 6 bytes. Compressing it would actually 
    /// make it larger (due to headers) and waste CPU cycles. Data smaller than 1 KB is stored as raw bytes, 
    /// while large lists and objects over 1 KB are automatically compressed.
    /// </remarks>
    public static int DefaultCompressionThresholdBytes { get; } = 1024;

    /// <summary>
    /// Gets the default compression level (<see cref="CompressionLevel.Fastest"/>).
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case (Sub-millisecond Latency):</b>
    /// In caching, fast response times (1-2 ms) are more important than achieving maximum compression. 
    /// The <c>Fastest</c> level compresses data in microseconds with minimal CPU usage, providing the optimal 
    /// balance between network savings and speed.
    /// </remarks>
    public static CompressionLevel DefaultCompressionLevel { get; } = CompressionLevel.Fastest;
}
