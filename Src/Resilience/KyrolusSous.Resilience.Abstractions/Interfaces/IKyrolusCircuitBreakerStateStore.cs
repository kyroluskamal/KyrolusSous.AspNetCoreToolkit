namespace KyrolusSous.Resilience;

/// <summary>
/// Distributed or in-memory persistence store for synchronizing circuit breaker states across microservice instances / pods.
/// </summary>
public interface IKyrolusCircuitBreakerStateStore
{
    /// <summary>
    /// Gets the shared circuit state for a pipeline.
    /// </summary>
    Task<KyrolusCircuitState> GetStateAsync(string pipelineName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the shared circuit state for a pipeline and broadcasts to other instances.
    /// </summary>
    Task SetStateAsync(string pipelineName, KyrolusCircuitState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event triggered when a remote instance updates a circuit state.
    /// </summary>
    event Action<string, KyrolusCircuitState>? OnRemoteStateChanged;
}
