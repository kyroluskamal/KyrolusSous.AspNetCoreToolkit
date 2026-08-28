namespace KyrolusSous.Resilience;

/// <summary>
/// Partitioned rate limiter that isolates concurrency limits per tenant, client IP, or user key.
/// </summary>
public interface IKyrolusPartitionedRateLimiter
{
    /// <summary>
    /// Attempts to acquire an execution permit for a given partition key (e.g. tenant ID).
    /// </summary>
    bool TryAcquire(string partitionKey);

    /// <summary>
    /// Releases an execution permit for the partition key.
    /// </summary>
    void Release(string partitionKey);
}
