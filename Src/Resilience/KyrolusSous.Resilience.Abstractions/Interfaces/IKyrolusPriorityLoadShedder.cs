namespace KyrolusSous.Resilience;

/// <summary>
/// Proactively sheds low-priority requests when system resource usage (CPU / Queue depth) reaches critical saturation.
/// </summary>
public interface IKyrolusPriorityLoadShedder
{
    /// <summary>
    /// Checks whether a request with the given priority should be rejected/shed to protect system stability.
    /// </summary>
    bool ShouldShed(KyrolusRequestPriority priority);

    /// <summary>
    /// Reports current CPU load percentage (0.0 to 100.0).
    /// </summary>
    void ReportCpuLoad(double cpuPercentage);

    /// <summary>
    /// Reports current request queue depth.
    /// </summary>
    void ReportQueueDepth(int depth);
}
