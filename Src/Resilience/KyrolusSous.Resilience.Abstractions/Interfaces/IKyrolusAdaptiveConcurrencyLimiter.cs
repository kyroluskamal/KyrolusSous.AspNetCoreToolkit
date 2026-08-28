namespace KyrolusSous.Resilience;

/// <summary>
/// Dynamic, gradient-based adaptive concurrency limiter that scales in-flight requests based on measured P99 latency.
/// </summary>
public interface IKyrolusAdaptiveConcurrencyLimiter
{
    /// <summary>
    /// Current calculated dynamic limit of concurrent requests.
    /// </summary>
    int CurrentLimit { get; }

    /// <summary>
    /// Current number of in-flight requests currently executing.
    /// </summary>
    int InFlightRequests { get; }

    /// <summary>
    /// Attempts to acquire a permit to execute a request.
    /// </summary>
    bool TryAcquire();

    /// <summary>
    /// Releases an acquired permit and records the execution duration to adjust the adaptive limit.
    /// </summary>
    void Release(TimeSpan executionDuration, bool success);
}
