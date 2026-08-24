namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Exception thrown when a cache operation is attempted while the Redis circuit breaker is in an OPEN state.
/// </summary>
public sealed class KyrolusRedisCircuitOpenException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusRedisCircuitOpenException"/>.
    /// </summary>
    /// <param name="retryAfter">The remaining duration before the circuit breaker allows a trial probe request.</param>
    public KyrolusRedisCircuitOpenException(TimeSpan? retryAfter)
        : base(retryAfter.HasValue
            ? $"Redis circuit is open. Retry after {retryAfter.Value.TotalSeconds:F1}s."
            : "Redis circuit is open.")
    {
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// Gets the estimated time duration remaining before the circuit breaker transitions to the Half-Open state.
    /// </summary>
    public TimeSpan? RetryAfter { get; }
}
