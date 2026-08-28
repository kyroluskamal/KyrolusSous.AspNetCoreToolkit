namespace KyrolusSous.Resilience;

/// <summary>
/// Chaos Engineering and Fault Injection Engine for testing system resilience and graceful degradation.
/// </summary>
public interface IKyrolusChaosEngine
{
    /// <summary>
    /// Evaluates chaos configuration and potentially injects artificial latency or simulated faults.
    /// </summary>
    ValueTask MaybeInjectFaultAsync(string pipelineName, CancellationToken cancellationToken = default);
}
