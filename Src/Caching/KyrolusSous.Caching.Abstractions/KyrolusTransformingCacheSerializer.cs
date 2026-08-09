namespace KyrolusSous.Caching.Abstractions;

public sealed class KyrolusTransformingCacheSerializer : IKyrolusCacheSerializer
{
    private readonly IKyrolusCacheSerializer inner;
    private readonly IKyrolusCachePayloadTransformer[] transformers;

    public KyrolusTransformingCacheSerializer(
        IKyrolusCacheSerializer inner,
        IEnumerable<IKyrolusCachePayloadTransformer> transformers)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(transformers);

        this.inner = inner;
        this.transformers = transformers as IKyrolusCachePayloadTransformer[] ?? transformers.ToArray();
    }

    public byte[] Serialize<T>(T value)
    {
        var payload = inner.Serialize(value);
        foreach (var transformer in transformers)
        {
            payload = transformer.Transform(payload);
        }

        return payload;
    }

    public T? Deserialize<T>(byte[] payload)
    {
        var data = payload;
        for (var index = transformers.Length - 1; index >= 0; index--)
        {
            data = transformers[index].Restore(data);
        }

        return inner.Deserialize<T>(data);
    }
}
