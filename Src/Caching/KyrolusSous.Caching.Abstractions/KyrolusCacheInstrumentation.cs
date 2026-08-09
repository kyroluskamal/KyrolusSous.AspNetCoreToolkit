using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace KyrolusSous.Caching.Abstractions;

public static class KyrolusCacheInstrumentation
{
    public const string ActivitySourceName = "KyrolusSous.Caching";
    public const string MeterName = "KyrolusSous.Caching";

    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName);
    public static Meter Meter { get; } = new(MeterName);

    public static Counter<long> HitCounter { get; } = Meter.CreateCounter<long>("kyrolus.cache.hits");
    public static Counter<long> MissCounter { get; } = Meter.CreateCounter<long>("kyrolus.cache.misses");
    public static Counter<long> SetCounter { get; } = Meter.CreateCounter<long>("kyrolus.cache.sets");
    public static Counter<long> RemoveCounter { get; } = Meter.CreateCounter<long>("kyrolus.cache.removes");
    public static Counter<long> ErrorCounter { get; } = Meter.CreateCounter<long>("kyrolus.cache.errors");
    public static Counter<long> LockAcquiredCounter { get; } = Meter.CreateCounter<long>("kyrolus.cache.locks.acquired");
    public static Counter<long> LockFailedCounter { get; } = Meter.CreateCounter<long>("kyrolus.cache.locks.failed");
    public static Histogram<double> LatencyMs { get; } = Meter.CreateHistogram<double>("kyrolus.cache.latency.ms");
    public static Histogram<double> LockWaitMs { get; } = Meter.CreateHistogram<double>("kyrolus.cache.lock.wait.ms");

    public static void RecordHit(KyrolusCacheOperation operation, string provider, long count = 1) =>
        HitCounter.Add(count, BuildTags(operation, provider));

    public static void RecordMiss(KyrolusCacheOperation operation, string provider, long count = 1) =>
        MissCounter.Add(count, BuildTags(operation, provider));

    public static void RecordSet(KyrolusCacheOperation operation, string provider, long count = 1) =>
        SetCounter.Add(count, BuildTags(operation, provider));

    public static void RecordRemove(KyrolusCacheOperation operation, string provider, long count = 1) =>
        RemoveCounter.Add(count, BuildTags(operation, provider));

    public static void RecordError(KyrolusCacheOperation operation, string provider) =>
        ErrorCounter.Add(1, BuildTags(operation, provider));

    public static void RecordLockAcquired(string provider) =>
        LockAcquiredCounter.Add(1, BuildTags(KyrolusCacheOperation.GetOrCreate, provider));

    public static void RecordLockFailed(string provider) =>
        LockFailedCounter.Add(1, BuildTags(KyrolusCacheOperation.GetOrCreate, provider));

    public static void RecordLatency(KyrolusCacheOperation operation, string provider, TimeSpan duration) =>
        LatencyMs.Record(duration.TotalMilliseconds, BuildTags(operation, provider));

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
