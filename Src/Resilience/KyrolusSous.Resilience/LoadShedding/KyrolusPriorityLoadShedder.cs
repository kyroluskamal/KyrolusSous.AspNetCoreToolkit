namespace KyrolusSous.Resilience;

/// <summary>
/// Thread-safe priority-based load shedder protecting server capacity from catastrophic overload.
/// </summary>
public class KyrolusPriorityLoadShedder : IKyrolusPriorityLoadShedder
{
    private double _cpuLoad;
    private int _queueDepth;
    private readonly Lock _lock = new();

    public double CpuLoadThresholdLow { get; set; } = 75.0;
    public double CpuLoadThresholdNormal { get; set; } = 85.0;
    public double CpuLoadThresholdHigh { get; set; } = 95.0;

    public int QueueDepthThresholdLow { get; set; } = 500;
    public int QueueDepthThresholdNormal { get; set; } = 1000;

    public void ReportCpuLoad(double cpuPercentage)
    {
        lock (_lock)
        {
            _cpuLoad = Math.Clamp(cpuPercentage, 0.0, 100.0);
        }
    }

    public void ReportQueueDepth(int depth)
    {
        lock (_lock)
        {
            _queueDepth = Math.Max(0, depth);
        }
    }

    public bool ShouldShed(KyrolusRequestPriority priority)
    {
        lock (_lock)
        {
            // Critical never shed unless hard system crash
            if (priority == KyrolusRequestPriority.Critical)
            {
                return false;
            }

            // High only shed when CPU > 95%
            if (priority == KyrolusRequestPriority.High)
            {
                return _cpuLoad >= CpuLoadThresholdHigh;
            }

            // Normal shed when CPU >= 85% or queue deep
            if (priority == KyrolusRequestPriority.Normal)
            {
                return _cpuLoad >= CpuLoadThresholdNormal || _queueDepth >= QueueDepthThresholdNormal;
            }

            // Low / Background shed early when CPU >= 75% or queue starting to back up
            return _cpuLoad >= CpuLoadThresholdLow || _queueDepth >= QueueDepthThresholdLow;
        }
    }
}
