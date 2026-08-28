using System.Collections.Concurrent;

namespace KyrolusSous.Resilience;

/// <summary>
/// Thread-safe adaptive timeout estimator calculating dynamic thresholds using moving average and standard deviation (μ + 3σ).
/// </summary>
public class KyrolusAdaptiveTimeoutEstimator : IKyrolusAdaptiveTimeoutEstimator
{
    private sealed class PipelineStats
    {
        public double MeanMs = 500.0;
        public double VarianceMs = 10000.0;
        public long SampleCount = 0;
        public readonly Lock Lock = new();
    }

    private readonly ConcurrentDictionary<string, PipelineStats> _stats = new(StringComparer.OrdinalIgnoreCase);

    public TimeSpan MinTimeout { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan MaxTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan GetDynamicTimeout(string pipelineName = "default")
    {
        var stat = _stats.GetOrAdd(pipelineName, _ => new PipelineStats());

        double mean, variance;
        lock (stat.Lock)
        {
            mean = stat.MeanMs;
            variance = stat.VarianceMs;
        }

        var stdDev = Math.Sqrt(Math.Max(0, variance));
        var timeoutMs = mean + (3.0 * stdDev);

        var dynamicTimeSpan = TimeSpan.FromMilliseconds(timeoutMs);
        return TimeSpan.FromMilliseconds(Math.Clamp(dynamicTimeSpan.TotalMilliseconds, MinTimeout.TotalMilliseconds, MaxTimeout.TotalMilliseconds));
    }

    public void RecordDuration(string pipelineName, TimeSpan duration)
    {
        var stat = _stats.GetOrAdd(pipelineName, _ => new PipelineStats());
        var sampleMs = duration.TotalMilliseconds;

        lock (stat.Lock)
        {
            stat.SampleCount++;
            var alpha = Math.Min(0.2, 1.0 / Math.Max(1, stat.SampleCount)); // Exponential smoothing

            var diff = sampleMs - stat.MeanMs;
            stat.MeanMs += alpha * diff;
            stat.VarianceMs = (1.0 - alpha) * (stat.VarianceMs + (alpha * diff * diff));
        }
    }
}
