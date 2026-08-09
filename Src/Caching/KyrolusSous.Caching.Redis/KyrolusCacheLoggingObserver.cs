using KyrolusSous.Caching.Abstractions;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.Caching.Redis;

public sealed class KyrolusCacheLoggingObserverOptions
{
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
    public bool LogHits { get; set; }
    public bool LogMisses { get; set; } = true;
    public bool LogSets { get; set; } = true;
    public bool LogRemoves { get; set; } = true;
    public bool LogExists { get; set; }
    public bool LogErrors { get; set; } = true;
    public bool LogLocks { get; set; }
}

public sealed class KyrolusCacheLoggingObserver : IKyrolusCacheObserver
{
    private readonly ILogger<KyrolusCacheLoggingObserver> logger;
    private readonly KyrolusCacheLoggingObserverOptions options;

    public KyrolusCacheLoggingObserver(
        ILogger<KyrolusCacheLoggingObserver> logger,
        KyrolusCacheLoggingObserverOptions? options = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options ?? new KyrolusCacheLoggingObserverOptions();
    }

    public Task OnObservationAsync(KyrolusCacheObserverContext context)
    {
        if (!ShouldLog(context.Observation) || !logger.IsEnabled(options.LogLevel))
        {
            return Task.CompletedTask;
        }

        var durationMs = context.Duration?.TotalMilliseconds ?? 0;
        logger.Log(
            options.LogLevel,
            context.Exception,
            "Cache {Observation} {Operation} key={Key} region={Region} tenant={TenantId} type={ValueType} durationMs={DurationMs}",
            context.Observation,
            context.Operation,
            context.Key,
            context.Region,
            context.TenantId,
            context.ValueType?.Name ?? "-",
            durationMs);
        return Task.CompletedTask;
    }

    private bool ShouldLog(KyrolusCacheObservation observation)
    {
        return observation switch
        {
            KyrolusCacheObservation.Hit => options.LogHits,
            KyrolusCacheObservation.Miss => options.LogMisses,
            KyrolusCacheObservation.Set => options.LogSets,
            KyrolusCacheObservation.Remove => options.LogRemoves,
            KyrolusCacheObservation.Exists => options.LogExists,
            KyrolusCacheObservation.Error => options.LogErrors,
            KyrolusCacheObservation.LockAcquired => options.LogLocks,
            KyrolusCacheObservation.LockFailed => options.LogLocks,
            _ => false
        };
    }
}
