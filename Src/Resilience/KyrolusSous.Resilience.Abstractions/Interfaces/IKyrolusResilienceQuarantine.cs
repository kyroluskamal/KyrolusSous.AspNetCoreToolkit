namespace KyrolusSous.Resilience;

/// <summary>
/// Quarantines repeated poison-pill requests (faulty keys/parameters) to prevent cascading circuit breaker trips.
/// </summary>
public interface IKyrolusResilienceQuarantine
{
    /// <summary>
    /// Checks if a specific request key is currently quarantined.
    /// </summary>
    bool IsQuarantined(string requestKey);

    /// <summary>
    /// Records a failed attempt for a request key, putting it into quarantine if it exceeds the failure threshold.
    /// </summary>
    void RecordFailure(string requestKey, int failureThreshold = 3, TimeSpan? quarantineDuration = null);

    /// <summary>
    /// Records a successful execution for a request key, resetting its failure streak.
    /// </summary>
    void RecordSuccess(string requestKey);
}
