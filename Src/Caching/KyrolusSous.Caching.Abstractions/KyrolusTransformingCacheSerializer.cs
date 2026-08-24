namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Orchestrates the end-to-end cache pipeline by chaining a root serializer (e.g. JSON) 
/// with an ordered sequence of payload transformers (e.g. Compression and Encryption).
/// </summary>
/// <remarks>
/// <b>Real-World Pipeline Flow:</b>
/// <para>
/// <b>1. Saving an Object (<see cref="Serialize{T}"/>):</b>
/// <list type="number">
///   <item><description><c>User Object</c> is converted to UTF-8 JSON bytes: <c>{"name":"Kyrolus","ssn":"1234"}</c> (e.g. 100 bytes).</description></item>
///   <item><description><b>Brotli Transformer (Order 10):</b> Compresses the 100 bytes down to 35 bytes.</description></item>
///   <item><description><b>AES-256 Transformer (Order 20):</b> Encrypts the 35 bytes into randomized ciphertext bytes <c>[0x8A, 0xFE...]</c>.</description></item>
///   <item><description>Final encrypted 48 bytes are stored safely into Redis.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>2. Loading an Object (<see cref="Deserialize{T}"/>):</b>
/// <list type="number">
///   <item><description>Reads encrypted 48 bytes from Redis.</description></item>
///   <item><description><b>AES-256 Transformer Restore:</b> Decrypts ciphertext back into the 35 compressed bytes.</description></item>
///   <item><description><b>Brotli Transformer Restore:</b> Decompresses 35 bytes back into the original 100 JSON bytes.</description></item>
///   <item><description><b>JSON Deserializer:</b> Reconstructs the strongly typed <c>User</c> C# object.</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class KyrolusTransformingCacheSerializer : IKyrolusCacheSerializer
{
    private readonly IKyrolusCacheSerializer inner;
    private readonly IKyrolusCachePayloadTransformer[] transformers;

    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusTransformingCacheSerializer"/>.
    /// </summary>
    /// <param name="inner">The root serializer (JSON or MessagePack).</param>
    /// <param name="transformers">The sequential list of transformers (e.g. Compression, Encryption).</param>
    public KyrolusTransformingCacheSerializer(
        IKyrolusCacheSerializer inner,
        IEnumerable<IKyrolusCachePayloadTransformer> transformers)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(transformers);

        this.inner = inner;
        this.transformers = transformers as IKyrolusCachePayloadTransformer[] ?? transformers.ToArray();
    }

    /// <summary>
    /// Serializes the object with the inner serializer and runs all transformers in forward order (e.g. Compress then Encrypt).
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="value">The object instance to serialize.</param>
    /// <returns>The fully transformed byte array to store in Redis.</returns>
    public byte[] Serialize<T>(T value)
    {
        var payload = inner.Serialize(value);
        foreach (var transformer in transformers)
        {
            payload = transformer.Transform(payload);
        }

        return payload;
    }

    /// <summary>
    /// Runs all transformers in reverse order (e.g. Decrypt then Decompress) and deserializes the original object.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="payload">The transformed byte array read from Redis.</param>
    /// <returns>The deserialized C# object.</returns>
    public T? Deserialize<T>(byte[] payload)
    {
        if (payload is null || payload.Length == 0)
        {
            return default;
        }

        var data = payload;
        for (var index = transformers.Length - 1; index >= 0; index--)
        {
            data = transformers[index].Restore(data);
        }

        return inner.Deserialize<T>(data);
    }
}
