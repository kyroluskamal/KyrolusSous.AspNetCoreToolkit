using System.Diagnostics;
using System.Diagnostics.Metrics;
using KyrolusSous.DataProtection.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusDataProtectionInstrumentation : IDisposable
{
    private readonly KyrolusDataProtectionInstrumentationOptions options;
    private readonly Counter<long> protectSuccess;
    private readonly Counter<long> protectFailure;
    private readonly Counter<long> unprotectSuccess;
    private readonly Counter<long> unprotectFailure;
    private readonly Histogram<double> durationMs;

    public KyrolusDataProtectionInstrumentation(IOptions<KyrolusDataProtectionInstrumentationOptions> options)
    {
        this.options = options?.Value ?? new KyrolusDataProtectionInstrumentationOptions();
        ActivitySource = new ActivitySource(this.options.ActivitySourceName);
        Meter = new Meter(this.options.MeterName);

        protectSuccess = Meter.CreateCounter<long>("kyrolus.dataprotection.protect.success");
        protectFailure = Meter.CreateCounter<long>("kyrolus.dataprotection.protect.failure");
        unprotectSuccess = Meter.CreateCounter<long>("kyrolus.dataprotection.unprotect.success");
        unprotectFailure = Meter.CreateCounter<long>("kyrolus.dataprotection.unprotect.failure");
        durationMs = Meter.CreateHistogram<double>("kyrolus.dataprotection.duration.ms");
    }

    public ActivitySource ActivitySource { get; }
    public Meter Meter { get; }

    public Activity? StartActivity(string operation)
    {
        if (!options.EnableActivities) return null;
        return ActivitySource.StartActivity($"kyrolus.dataprotection.{operation}");
    }

    public void RecordSuccess(string operation, double elapsedMs)
    {
        if (!options.EnableMetrics) return;

        if (string.Equals(operation, "protect", StringComparison.OrdinalIgnoreCase))
        {
            protectSuccess.Add(1);
        }
        else
        {
            unprotectSuccess.Add(1);
        }

        durationMs.Record(elapsedMs, new KeyValuePair<string, object?>("operation", operation));
    }

    public void RecordFailure(string operation, double elapsedMs)
    {
        if (!options.EnableMetrics) return;

        if (string.Equals(operation, "protect", StringComparison.OrdinalIgnoreCase))
        {
            protectFailure.Add(1);
        }
        else
        {
            unprotectFailure.Add(1);
        }

        durationMs.Record(elapsedMs, new KeyValuePair<string, object?>("operation", operation));
    }

    public void Dispose()
    {
        ActivitySource.Dispose();
        Meter.Dispose();
    }
}
