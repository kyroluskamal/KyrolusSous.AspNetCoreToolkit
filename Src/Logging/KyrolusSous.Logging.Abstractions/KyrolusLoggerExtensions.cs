using KyrolusSous.Logging.Abstractions.Timers;

namespace KyrolusSous.Logging.Abstractions;

/// <summary>
/// Convenient structured extension methods for <see cref="IKyrolusLogger"/>.
/// </summary>
public static class KyrolusLoggerExtensions
{
    /// <summary>
    /// Starts a timed operation measuring execution duration and logging the result upon disposal.
    /// </summary>
    public static IDisposable BeginTimedOperation(
        this IKyrolusLogger logger,
        string operationName,
        double? warnIfExceedsMs = null,
        LogLevel level = LogLevel.Information)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return new KyrolusTimedOperation(logger, operationName, warnIfExceedsMs, level);
    }

    /// <summary>
    /// Begins a structured log scope with key-value pairs.
    /// </summary>
    public static IDisposable? BeginScope(this IKyrolusLogger logger, string key, object? value)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return logger.BeginScope(new Dictionary<string, object?> { [key] = value });
    }

    /// <summary>
    /// Logs a message at the <see cref="LogLevel.Trace"/> level.
    /// </summary>
    public static void LogTrace(this IKyrolusLogger logger, string message, IReadOnlyDictionary<string, object?>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.Log(LogLevel.Trace, message, null, properties);
    }

    /// <summary>
    /// Logs a message at the <see cref="LogLevel.Debug"/> level.
    /// </summary>
    public static void LogDebug(this IKyrolusLogger logger, string message, IReadOnlyDictionary<string, object?>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.Log(LogLevel.Debug, message, null, properties);
    }

    /// <summary>
    /// Logs a message at the <see cref="LogLevel.Information"/> level.
    /// </summary>
    public static void LogInformation(this IKyrolusLogger logger, string message, IReadOnlyDictionary<string, object?>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.Log(LogLevel.Information, message, null, properties);
    }

    /// <summary>
    /// Logs a message at the <see cref="LogLevel.Warning"/> level.
    /// </summary>
    public static void LogWarning(this IKyrolusLogger logger, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.Log(LogLevel.Warning, message, exception, properties);
    }

    /// <summary>
    /// Logs a message at the <see cref="LogLevel.Error"/> level.
    /// </summary>
    public static void LogError(this IKyrolusLogger logger, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.Log(LogLevel.Error, message, exception, properties);
    }

    /// <summary>
    /// Logs a message at the <see cref="LogLevel.Critical"/> level.
    /// </summary>
    public static void LogCritical(this IKyrolusLogger logger, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        logger.Log(LogLevel.Critical, message, exception, properties);
    }
}
