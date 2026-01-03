namespace KyrolusSous.Caching.Abstractions;

public sealed class KyrolusOrderedCachePayloadTransformer : IKyrolusOrderedCachePayloadTransformer
{
    private readonly IKyrolusCachePayloadTransformer inner;

    public KyrolusOrderedCachePayloadTransformer(IKyrolusCachePayloadTransformer inner, int order)
    {
        ArgumentNullException.ThrowIfNull(inner);
        this.inner = inner;
        Order = order;
    }

    public int Order { get; }

    public byte[] Transform(byte[] payload) => inner.Transform(payload);

    public byte[] Restore(byte[] payload) => inner.Restore(payload);
}
