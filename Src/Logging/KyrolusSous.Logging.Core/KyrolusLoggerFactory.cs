using KyrolusSous.Logging.Core.Masking;

namespace KyrolusSous.Logging.Core;

internal sealed class KyrolusLoggerFactory(ILoggerFactory inner, IKyrolusDataMasker? masker = null) : IKyrolusLoggerFactory
{
    private readonly ILoggerFactory _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly IKyrolusDataMasker _masker = masker ?? new KyrolusSensitiveDataMasker();

    public IKyrolusLogger Create(string categoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        return new KyrolusLogger(_inner.CreateLogger(categoryName), _masker);
    }

    public IKyrolusLogger<TCategory> Create<TCategory>() => new KyrolusLogger<TCategory>(_inner.CreateLogger<TCategory>(), _masker);
}
