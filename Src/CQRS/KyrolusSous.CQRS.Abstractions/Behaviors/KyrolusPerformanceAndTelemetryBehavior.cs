using System.Diagnostics;
using KyrolusSous.CQRS.Abstractions.Telemetry;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.Abstractions.Behaviors;

/// <summary>
/// Configuration options for CQRS performance logging and telemetry.
/// </summary>
public sealed class KyrolusCqrsPerformanceOptions
{
    /// <summary>
    /// Whether telemetry and performance tracking are enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Threshold in milliseconds above which a request is logged as a slow request warning. Default: 500ms.
    /// </summary>
    public long SlowRequestThresholdMs { get; set; } = 500;

    /// <summary>
    /// Whether to log execution metrics for every request at Trace or Debug level. Default: true.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = true;
}

/// <summary>
/// Pipeline behavior providing OpenTelemetry tracing, execution time measurement, and slow-request detection.
/// </summary>
[PipelineOrder(-900)]
public sealed class KyrolusPerformanceAndTelemetryBehavior<TRequest, TResponse>(
    ILogger<KyrolusPerformanceAndTelemetryBehavior<TRequest, TResponse>>? logger = null,
    KyrolusCqrsPerformanceOptions? options = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger? _logger = logger;
    private readonly KyrolusCqrsPerformanceOptions _options = options ?? new();

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (!_options.Enabled)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var requestName = typeof(TRequest).Name;
        var isCommand = request is IKyrolusCommandBase;
        var requestKind = isCommand ? "Command" : "Query";

        using var activity = KyrolusCqrsTelemetry.ActivitySource.StartActivity($"CQRS {requestName}");
        activity?.SetTag(KyrolusCqrsTelemetry.TagRequestType, typeof(TRequest).FullName);
        activity?.SetTag(KyrolusCqrsTelemetry.TagRequestKind, requestKind);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);
            sw.Stop();

            var elapsedMs = sw.ElapsedMilliseconds;
            activity?.SetTag(KyrolusCqrsTelemetry.TagExecutionDurationMs, elapsedMs);

            if (elapsedMs > _options.SlowRequestThresholdMs)
            {
                activity?.SetTag(KyrolusCqrsTelemetry.TagSlowRequest, true);
                _logger?.LogWarning(
                    "[Kyrolus CQRS] Slow {RequestKind} detected: {RequestName} took {ElapsedMs}ms (Threshold: {ThresholdMs}ms)",
                    requestKind,
                    requestName,
                    elapsedMs,
                    _options.SlowRequestThresholdMs);
            }
            else if (_options.EnableDetailedLogging)
            {
                _logger?.LogDebug(
                    "[Kyrolus CQRS] Executed {RequestKind} {RequestName} in {ElapsedMs}ms",
                    requestKind,
                    requestName,
                    elapsedMs);
            }

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag(KyrolusCqrsTelemetry.TagExecutionDurationMs, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
