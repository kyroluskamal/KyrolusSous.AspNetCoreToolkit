using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace KyrolusSous.Repositories.EF.Abstractions.Observability;

public static class KyrolusRepositoryInstrumentation
{
    public const string ActivitySourceName = "KyrolusSous.Repositories.EF";
    public const string MeterName = "KyrolusSous.Repositories.EF";

    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName);
    public static Meter Meter { get; } = new(MeterName);

    public static Counter<long> OperationCounter { get; } = Meter.CreateCounter<long>("kyrolus.repo.operations");
    public static Counter<long> ErrorCounter { get; } = Meter.CreateCounter<long>("kyrolus.repo.errors");
    public static Histogram<double> DurationMs { get; } = Meter.CreateHistogram<double>("kyrolus.repo.duration.ms");

    public static void RecordOperation(string operation, bool success)
        => OperationCounter.Add(1, BuildTags(operation, success));

    public static void RecordError(string operation)
        => ErrorCounter.Add(1, BuildTags(operation, success: false));

    public static void RecordDuration(string operation, TimeSpan duration, bool success)
        => DurationMs.Record(duration.TotalMilliseconds, BuildTags(operation, success));

    private static TagList BuildTags(string operation, bool success)
        => new()
        {
            { "operation", operation },
            { "success", success }
        };
}
