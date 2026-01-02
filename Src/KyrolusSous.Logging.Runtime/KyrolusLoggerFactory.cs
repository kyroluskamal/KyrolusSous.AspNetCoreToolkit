namespace KyrolusSous.Logging.Runtime;

internal sealed class KyrolusLoggerFactory(ILoggerFactory inner) : IKyrolusLoggerFactory
{
    private readonly ILoggerFactory inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public IKyrolusLogger Create(string categoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        return new KyrolusLogger(inner.CreateLogger(categoryName));
    }

    public IKyrolusLogger<TCategory> Create<TCategory>() => new KyrolusLogger<TCategory>(inner.CreateLogger<TCategory>());
}
