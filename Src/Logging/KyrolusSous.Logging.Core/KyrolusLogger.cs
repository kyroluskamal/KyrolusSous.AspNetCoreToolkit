using KyrolusSous.Logging.Core.Masking;

namespace KyrolusSous.Logging.Core;

internal sealed class KyrolusLogger(ILogger inner, IKyrolusDataMasker? masker = null) : IKyrolusLogger
{
    private readonly ILogger _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly IKyrolusDataMasker _masker = masker ?? new KyrolusSensitiveDataMasker();

    public bool IsEnabled(LogLevel level) => _inner.IsEnabled(level);

    public IDisposable? BeginScope(IReadOnlyDictionary<string, object?> values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        var sanitized = _masker.SanitizeProperties(values);
        return _inner.BeginScope(sanitized);
    }

    public void Log(LogLevel level, string message, Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (properties is null || properties.Count == 0)
        {
            _inner.Log(level, exception, message);
            return;
        }

        var sanitized = _masker.SanitizeProperties(properties);
        using var scope = _inner.BeginScope(sanitized);
        _inner.Log(level, exception, message);
    }
}
