using System.Collections.Concurrent;
using System.Diagnostics;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using KyrolusSous.Repositories.EF.Abstractions.Observability;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.Repositories.EF.Runtime.Observability;

public sealed class KyrolusRepositoryTelemetryObserverOptions
{
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
    public TimeSpan? SlowThreshold { get; set; } = TimeSpan.FromMilliseconds(200);
    public bool LogPayloadType { get; set; }
    public bool LogErrors { get; set; } = true;
}

public sealed class KyrolusRepositoryTelemetryObserver : IKyrolusRepositoryObserver
{
    private readonly ILogger<KyrolusRepositoryTelemetryObserver> logger;
    private readonly KyrolusRepositoryTelemetryObserverOptions options;
    private readonly AsyncLocal<ConcurrentStack<Activity>> activityStack = new();

    public KyrolusRepositoryTelemetryObserver(
        ILogger<KyrolusRepositoryTelemetryObserver> logger,
        KyrolusRepositoryTelemetryObserverOptions? options = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options ?? new KyrolusRepositoryTelemetryObserverOptions();
    }

    public Task OnBeforeAsync(string operation, object? payload, CancellationToken cancellationToken = default)
    {
        var activity = KyrolusRepositoryInstrumentation.ActivitySource.StartActivity($"repo.{operation}", ActivityKind.Internal);
        if (activity is not null)
        {
            activity.SetTag("repo.operation", operation);
            if (options.LogPayloadType && payload is not null)
                activity.SetTag("repo.payload.type", payload.GetType().FullName);
            var stack = activityStack.Value ??= new ConcurrentStack<Activity>();
            stack.Push(activity);
        }

        return Task.CompletedTask;
    }

    public Task OnAfterAsync(string operation, object? payload, TimeSpan? duration = null, Exception? exception = null, CancellationToken cancellationToken = default)
    {
        var stack = activityStack.Value;
        if (stack is not null && stack.TryPop(out var activity))
        {
            if (duration.HasValue) activity.SetTag("repo.duration.ms", duration.Value.TotalMilliseconds);
            if (exception is not null)
            {
                activity.SetStatus(ActivityStatusCode.Error, exception.Message);
                activity.SetTag("exception.type", exception.GetType().FullName);
                activity.SetTag("exception.message", exception.Message);
            }
            else
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }
            activity.Dispose();
        }

        if (duration.HasValue)
        {
            var success = exception is null;
            KyrolusRepositoryInstrumentation.RecordOperation(operation, success);
            KyrolusRepositoryInstrumentation.RecordDuration(operation, duration.Value, success);
            if (!success) KyrolusRepositoryInstrumentation.RecordError(operation);

            if (ShouldLog(duration.Value, exception) && logger.IsEnabled(options.LogLevel))
                logger.Log(
                    options.LogLevel,
                    exception,
                    "Repo operation {Operation} durationMs={DurationMs} success={Success}",
                    operation,
                    duration.Value.TotalMilliseconds,
                    success);
        }

        return Task.CompletedTask;
    }

    private bool ShouldLog(TimeSpan duration, Exception? exception)
    {
        if (exception is not null && options.LogErrors) return true;
        if (options.SlowThreshold is null) return true;
        return duration >= options.SlowThreshold.Value;
    }
}
