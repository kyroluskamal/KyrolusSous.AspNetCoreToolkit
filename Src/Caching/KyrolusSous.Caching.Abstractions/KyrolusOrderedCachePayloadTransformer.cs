namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Wraps an un-ordered <see cref="IKyrolusCachePayloadTransformer"/> with an explicit execution order in the serialization pipeline.
/// </summary>
/// <remarks>
/// <b>Real-World Use Case:</b>
/// Ensures that arbitrary third-party or custom transformers can be assigned an exact priority slot 
/// (e.g. running after Compression at Order 10 and before Encryption at Order 20).
/// </remarks>
public sealed class KyrolusOrderedCachePayloadTransformer : IKyrolusOrderedCachePayloadTransformer
{
    private readonly IKyrolusCachePayloadTransformer inner;

    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusOrderedCachePayloadTransformer"/>.
    /// </summary>
    /// <param name="inner">The underlying payload transformer.</param>
    /// <param name="order">The execution priority order.</param>
    public KyrolusOrderedCachePayloadTransformer(IKyrolusCachePayloadTransformer inner, int order)
    {
        ArgumentNullException.ThrowIfNull(inner);
        this.inner = inner;
        Order = order;
    }

    /// <inheritdoc />
    public int Order { get; }

    /// <inheritdoc />
    public byte[] Transform(byte[] payload) => inner.Transform(payload);

    /// <inheritdoc />
    public byte[] Restore(byte[] payload) => inner.Restore(payload);
}
