using KyrolusSous.Logging.Core.Masking;

namespace KyrolusSous.Logging.Core;

internal sealed class KyrolusLogger<TCategory>(ILogger<TCategory> inner, IKyrolusDataMasker? masker = null) : IKyrolusLogger<TCategory>
{
    private readonly IKyrolusLogger _logger = new KyrolusLogger(inner, masker);

    public bool IsEnabled(LogLevel level) => _logger.IsEnabled(level);

    public IDisposable? BeginScope(IReadOnlyDictionary<string, object?> values) => _logger.BeginScope(values);

    public void Log(LogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null)
        => _logger.Log(level, message, exception, properties);
}
