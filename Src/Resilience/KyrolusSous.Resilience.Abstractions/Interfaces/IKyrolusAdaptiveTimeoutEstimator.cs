namespace KyrolusSous.Resilience;

/// <summary>
/// Dynamically calculates optimal execution timeouts based on moving percentile latency distributions.
/// </summary>
public interface IKyrolusAdaptiveTimeoutEstimator
{
    /// <summary>
    /// Computes the dynamic timeout threshold for a named pipeline.
    /// </summary>
    TimeSpan GetDynamicTimeout(string pipelineName = "default");

    /// <summary>
    /// Records an execution duration sample to update moving latency statistics.
    /// </summary>
    void RecordDuration(string pipelineName, TimeSpan duration);
}
