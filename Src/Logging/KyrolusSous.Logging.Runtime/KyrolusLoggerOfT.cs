namespace KyrolusSous.Logging.Runtime;

internal sealed class KyrolusLogger<TCategory>(ILogger<TCategory> inner) : IKyrolusLogger<TCategory>
{
    private readonly IKyrolusLogger logger = new KyrolusLogger(inner);

    public bool IsEnabled(LogLevel level) => logger.IsEnabled(level);

    public IDisposable? BeginScope(IReadOnlyDictionary<string, object?> values) => logger.BeginScope(values);

    public void Log(LogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null)
        => logger.Log(level, message, exception, properties);
}
