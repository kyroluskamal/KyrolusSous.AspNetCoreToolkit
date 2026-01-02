namespace KyrolusSous.Logging.Abstractions;

public interface IKyrolusLogger
{
    bool IsEnabled(LogLevel level);

    IDisposable? BeginScope(IReadOnlyDictionary<string, object?> values);

    void Log(LogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null);
}

public interface IKyrolusLogger<out TCategory> : IKyrolusLogger
{
}
