namespace KyrolusSous.Resilience;

/// <summary>
/// Detailed metadata and diagnostic information for a named circuit breaker.
/// </summary>
public sealed record KyrolusCircuitBreakerInfo(
    string PipelineName,
    KyrolusCircuitState State,
    DateTimeOffset LastStateChangeUtc,
    long TotalRequests,
    long TotalFailures,
    double FailureRatio);

/// <summary>
/// Observer for monitoring, inspecting, and manually overriding circuit breaker states across resilience pipelines.
/// </summary>
public interface IKyrolusCircuitBreakerObserver
{
    /// <summary>
    /// Gets the current state of a circuit breaker for a specified pipeline name.
    /// </summary>
    KyrolusCircuitState GetCircuitState(string pipelineName = "default");

    /// <summary>
    /// Gets detailed diagnostic info for a specified pipeline.
    /// </summary>
    KyrolusCircuitBreakerInfo GetCircuitInfo(string pipelineName = "default");

    /// <summary>
    /// Gets all registered circuit breaker states across all pipelines.
    /// </summary>
    IReadOnlyDictionary<string, KyrolusCircuitState> GetAllCircuitStates();

    /// <summary>
    /// Manually forces a circuit breaker to the Open state (blocking traffic).
    /// </summary>
    void ForceOpen(string pipelineName);

    /// <summary>
    /// Manually forces a circuit breaker to the Closed state (allowing traffic).
    /// </summary>
    void ForceClose(string pipelineName);

    /// <summary>
    /// Resets a circuit breaker back to normal operational state and clears failure metrics.
    /// </summary>
    void Reset(string pipelineName);

    /// <summary>
    /// Event triggered whenever any circuit breaker changes state (e.g. from Closed to Open).
    /// </summary>
    event Action<string, KyrolusCircuitState>? OnCircuitStateChanged;
}
