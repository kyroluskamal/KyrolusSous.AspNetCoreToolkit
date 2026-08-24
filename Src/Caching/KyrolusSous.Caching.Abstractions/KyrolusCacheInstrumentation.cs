namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Provides OpenTelemetry metrics (Meters) and distributed tracing (ActivitySource) instrumentation 
/// for the caching subsystem.
/// </summary>
/// <remarks>
/// Exposes standard counters and histograms for metrics collection in Prometheus, Grafana, 
/// Datadog, or Azure Application Insights.
/// </remarks>
public static class KyrolusCacheInstrumentation
{
    /// <summary>
    /// The OpenTelemetry ActivitySource name for distributed cache tracing.
    /// </summary>
    public const string ActivitySourceName = "KyrolusSous.Caching";

    /// <summary>
    /// The OpenTelemetry Meter name for cache metrics collection.
    /// </summary>
    public const string MeterName = "KyrolusSous.Caching";

    /// <summary>
    /// Gets the shared <see cref="ActivitySource"/> used to create distributed tracing spans for cache operations.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName);

    /// <summary>
    /// Gets the shared <see cref="Meter"/> used to emit OpenTelemetry metrics.
    /// </summary>
    public static Meter Meter { get; } = new(MeterName);

    /// <summary>
    /// Counter tracking the total number of successful cache hits (<c>kyrolus.cache.hits</c>).
    /// </summary>
    public static Counter<long> HitCounter { get; } = Meter.CreateCounter<long>("kyrolus.cache.hits");

    /// <summary>
    /// Counter tracking the total number of cache misses (<c>kyrolus.cache.misses</c>).
    /// </summary>
    public static Counter<long> MissCounter { get; } = Meter.CreateCounter<long>("kyrolus.cache.misses");

    /// <summary>
    /// Counter tracking the total number of write/set operations (<c>kyrolus.cache.sets</c>).
    /// </summary>
    public static Counter<long> SetCounter { get; } = Meter.CreateCounter<long>("kyrolus.cache.sets");

    /// <summary>
    /// Counter tracking the total number of eviction/remove operations (<c>kyrolus.cache.removes</c>).
    /// </summary>
    public static Counter<long> RemoveCounter { get; } = Meter.CreateCounter<long>("kyrolus.cache.removes");

    /// <summary>
    /// Counter tracking cache operation errors and exceptions (<c>kyrolus.cache.errors</c>).
    /// </summary>
    public static Counter<long> ErrorCounter { get; } = Meter.CreateCounter<long>("kyrolus.cache.errors");

    /// <summary>
    /// Counter tracking successfully acquired distributed locks (<c>kyrolus.cache.locks.acquired</c>).
    /// </summary>
    public static Counter<long> LockAcquiredCounter { get; } = Meter.CreateCounter<long>("kyrolus.cache.locks.acquired");

    /// <summary>
    /// Counter tracking failed distributed lock acquisition attempts (<c>kyrolus.cache.locks.failed</c>).
    /// </summary>
    public static Counter<long> LockFailedCounter { get; } = Meter.CreateCounter<long>("kyrolus.cache.locks.failed");

    /// <summary>
    /// Histogram measuring operation latency in milliseconds (<c>kyrolus.cache.latency.ms</c>).
    /// </summary>
    public static Histogram<double> LatencyMs { get; } = Meter.CreateHistogram<double>("kyrolus.cache.latency.ms");

    /// <summary>
    /// Histogram measuring distributed lock wait time in milliseconds (<c>kyrolus.cache.lock.wait.ms</c>).
    /// </summary>
    public static Histogram<double> LockWaitMs { get; } = Meter.CreateHistogram<double>("kyrolus.cache.lock.wait.ms");

    /// <summary>
    /// Records a cache hit metric.
    /// </summary>
    /// <param name="operation">The type of cache operation.</param>
    /// <param name="provider">The name of the underlying cache provider (e.g. "Redis", "Memory").</param>
    /// <param name="count">The hit count to increment (defaults to 1).</param>
    public static void RecordHit(KyrolusCacheOperation operation, string provider, long count = 1) =>
        HitCounter.Add(count, BuildTags(operation, provider));

    /// <summary>
    /// Records a cache miss metric.
    /// </summary>
    /// <param name="operation">The type of cache operation.</param>
    /// <param name="provider">The name of the underlying cache provider.</param>
    /// <param name="count">The miss count to increment (defaults to 1).</param>
    public static void RecordMiss(KyrolusCacheOperation operation, string provider, long count = 1) =>
        MissCounter.Add(count, BuildTags(operation, provider));

    /// <summary>
    /// Records a cache write/set metric.
    /// </summary>
    /// <param name="operation">The type of cache operation.</param>
    /// <param name="provider">The name of the cache provider.</param>
    /// <param name="count">The number of items written (defaults to 1).</param>
    public static void RecordSet(KyrolusCacheOperation operation, string provider, long count = 1) =>
        SetCounter.Add(count, BuildTags(operation, provider));

    /// <summary>
    /// Records a cache eviction/removal metric.
    /// </summary>
    /// <param name="operation">The type of cache operation.</param>
    /// <param name="provider">The name of the cache provider.</param>
    /// <param name="count">The number of items removed (defaults to 1).</param>
    public static void RecordRemove(KyrolusCacheOperation operation, string provider, long count = 1) =>
        RemoveCounter.Add(count, BuildTags(operation, provider));

    /// <summary>
    /// Records a cache operation error metric.
    /// </summary>
    /// <param name="operation">The type of cache operation.</param>
    /// <param name="provider">The name of the cache provider.</param>
    public static void RecordError(KyrolusCacheOperation operation, string provider) =>
        ErrorCounter.Add(1, BuildTags(operation, provider));

    /// <summary>
    /// Records a successfully acquired distributed lock.
    /// </summary>
    /// <param name="provider">The cache provider name.</param>
    public static void RecordLockAcquired(string provider) =>
        LockAcquiredCounter.Add(1, BuildTags(KyrolusCacheOperation.GetOrCreate, provider));

    /// <summary>
    /// Records a failed distributed lock acquisition attempt.
    /// </summary>
    /// <param name="provider">The cache provider name.</param>
    public static void RecordLockFailed(string provider) =>
        LockFailedCounter.Add(1, BuildTags(KyrolusCacheOperation.GetOrCreate, provider));

    /// <summary>
    /// Records execution latency for a cache operation.
    /// </summary>
    /// <param name="operation">The type of cache operation.</param>
    /// <param name="provider">The cache provider name.</param>
    /// <param name="duration">The measured execution duration.</param>
    public static void RecordLatency(KyrolusCacheOperation operation, string provider, TimeSpan duration) =>
        LatencyMs.Record(duration.TotalMilliseconds, BuildTags(operation, provider));

    /// <summary>
    /// Records time spent waiting to acquire a distributed lock.
    /// </summary>
    /// <param name="provider">The cache provider name.</param>
    /// <param name="duration">The measured wait duration.</param>
    public static void RecordLockWait(string provider, TimeSpan duration) =>
        LockWaitMs.Record(duration.TotalMilliseconds, BuildTags(KyrolusCacheOperation.GetOrCreate, provider));

    private static TagList BuildTags(KyrolusCacheOperation operation, string provider)
    {
        var tags = new TagList
        {
            { "operation", operation.ToString() },
            { "provider", provider }
        };
        return tags;
    }
}
