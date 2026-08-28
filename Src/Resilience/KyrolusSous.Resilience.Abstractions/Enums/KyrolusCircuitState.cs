namespace KyrolusSous.Resilience;

/// <summary>
/// State of a circuit breaker.
/// </summary>
public enum KyrolusCircuitState
{
    /// <summary>
    /// Circuit is operating normally and allowing requests through.
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit has tripped due to failures and is blocking requests.
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is testing if downstream service has recovered by allowing a trial request.
    /// </summary>
    HalfOpen,

    /// <summary>
    /// Circuit is manually isolated and forced open.
    /// </summary>
    Isolated
}
