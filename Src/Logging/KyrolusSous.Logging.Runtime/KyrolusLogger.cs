namespace KyrolusSous.Logging.Runtime;

internal sealed class KyrolusLogger(ILogger inner) : IKyrolusLogger
{
    private readonly ILogger inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public bool IsEnabled(LogLevel level) => inner.IsEnabled(level);

    public IDisposable? BeginScope(IReadOnlyDictionary<string, object?> values)
    {
        if (values.Count == 0)
        {
            return null;
        }

        return inner.BeginScope(values);
    }

    public void Log(LogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (properties is null || properties.Count == 0)
        {
            inner.Log(level, exception, message);
            return;
        }

        using var scope = inner.BeginScope(properties);
        inner.Log(level, exception, message);
    }
}
