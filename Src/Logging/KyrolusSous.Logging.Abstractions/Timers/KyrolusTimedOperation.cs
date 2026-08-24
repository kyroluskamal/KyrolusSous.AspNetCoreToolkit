using System.Diagnostics;

namespace KyrolusSous.Logging.Abstractions.Timers;

/// <summary>
/// High-precision diagnostic timer that measures the execution duration of an operation and automatically logs the result upon disposal.
/// </summary>
public sealed class KyrolusTimedOperation : IDisposable
{
    private readonly IKyrolusLogger _logger;
    private readonly string _operationName;
    private readonly double? _warnIfExceedsMs;
    private readonly LogLevel _defaultLevel;
    private readonly long _startTimestamp;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="KyrolusTimedOperation"/> class.
    /// </summary>
    public KyrolusTimedOperation(
        IKyrolusLogger logger,
        string operationName,
        double? warnIfExceedsMs = null,
        LogLevel defaultLevel = LogLevel.Information)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _operationName = string.IsNullOrWhiteSpace(operationName) ? "Operation" : operationName;
        _warnIfExceedsMs = warnIfExceedsMs;
        _defaultLevel = defaultLevel;
        _startTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Completes the timed operation, calculates the elapsed time, and writes the structured log event.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
        var elapsedMs = elapsed.TotalMilliseconds;
        var exceeded = _warnIfExceedsMs.HasValue && elapsedMs >= _warnIfExceedsMs.Value;

        var level = exceeded ? LogLevel.Warning : _defaultLevel;
        var message = exceeded
            ? $"Slow operation detected: '{_operationName}' completed in {elapsedMs:F2}ms (exceeded threshold of {_warnIfExceedsMs!.Value:F2}ms)"
            : $"Operation '{_operationName}' completed in {elapsedMs:F2}ms";

        var properties = new Dictionary<string, object?>
        {
            ["OperationName"] = _operationName,
            ["ElapsedMilliseconds"] = elapsedMs,
            ["ExceededThreshold"] = exceeded
        };

        if (_warnIfExceedsMs.HasValue)
        {
            properties["ThresholdMilliseconds"] = _warnIfExceedsMs.Value;
        }

        _logger.Log(level, message, null, properties);
    }
}
