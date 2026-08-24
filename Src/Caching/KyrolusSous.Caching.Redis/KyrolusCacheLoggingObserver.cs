namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Configures filtering options for structured cache event logging (e.g., controlling which events are logged to console/Elasticsearch).
/// </summary>
public sealed class KyrolusCacheLoggingObserverOptions
{
    /// <summary>
    /// Gets or sets the <see cref="Microsoft.Extensions.Logging.LogLevel"/> used when logging cache events. Defaults to <see cref="LogLevel.Information"/>.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets whether to log cache Hits. Defaults to <c>false</c> to avoid excessive log volume on high-traffic servers.
    /// </summary>
    public bool LogHits { get; set; }

    /// <summary>
    /// Gets or sets whether to log cache Misses. Defaults to <c>true</c>.
    /// </summary>
    public bool LogMisses { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log cache Writes (Set operations). Defaults to <c>true</c>.
    /// </summary>
    public bool LogSets { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log cache Removals. Defaults to <c>true</c>.
    /// </summary>
    public bool LogRemoves { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log key existence checks. Defaults to <c>false</c>.
    /// </summary>
    public bool LogExists { get; set; }

    /// <summary>
    /// Gets or sets whether to log cache operational errors and exceptions. Defaults to <c>true</c>.
    /// </summary>
    public bool LogErrors { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log distributed lock acquisitions and failures. Defaults to <c>false</c>.
    /// </summary>
    public bool LogLocks { get; set; }
}

/// <summary>
/// Structured logging observer that writes cache operational diagnostics to <see cref="ILogger{TCategoryName}"/>.
/// </summary>
/// <remarks>
/// <b>Real-World Use Case:</b>
/// Emits structured JSON log entries into centralized log aggregators (Seq, Elasticsearch, Datadog) 
/// to monitor cache hit ratios, detect hot key misses, and audit cache-related exceptions in real time.
/// </remarks>
public sealed class KyrolusCacheLoggingObserver : IKyrolusCacheObserver
{
    private readonly ILogger<KyrolusCacheLoggingObserver> logger;
    private readonly KyrolusCacheLoggingObserverOptions options;

    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusCacheLoggingObserver"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Optional filtering options.</param>
    public KyrolusCacheLoggingObserver(
        ILogger<KyrolusCacheLoggingObserver> logger,
        KyrolusCacheLoggingObserverOptions? options = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options ?? new KyrolusCacheLoggingObserverOptions();
    }

    /// <inheritdoc />
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
