namespace KyrolusSous.Caching.MessagePack;

/// <summary>
/// A high-throughput binary cache serializer implementing <see cref="IKyrolusCacheSerializer"/> using MessagePack.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is MessagePack?</b>
/// MessagePack is an ultra-fast binary serialization format that functions like JSON but produces much smaller 
/// payloads (typically 30%–60% smaller than JSON) and serializes up to 5x faster with minimal memory allocations.
/// </para>
/// <para>
/// <b>Real-World Use Cases:</b>
/// <list type="bullet">
///   <item><description><b>High-Frequency Trading &amp; Real-Time Gaming:</b> Serializing telemetry, player coordinates, or market tick data where sub-millisecond serialization is mandatory.</description></item>
///   <item><description><b>Large Object Graphs:</b> Storing complex domain models and deep object hierarchies into Redis with significantly reduced bandwidth and memory usage.</description></item>
///   <item><description><b>Contractless POCO Support:</b> Uses <see cref="ContractlessStandardResolver"/> by default, meaning any standard C# class or record can be cached immediately without adding annotations or attributes.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Registering MessagePack serializer with ASP.NET Core Dependency Injection
/// builder.Services.AddKyrolusMessagePackSerializer();
/// </code>
/// </example>
public sealed class KyrolusMessagePackCacheSerializer : IKyrolusCacheSerializer
{
    private readonly MessagePackSerializerOptions options;

    /// <summary>
    /// Initializes a new instance with standard contractless resolver options, enabling seamless POCO serialization.
    /// </summary>
    public KyrolusMessagePackCacheSerializer()
        : this(ContractlessStandardResolver.Options)
    {
    }

    /// <summary>
    /// Initializes a new instance with custom <see cref="MessagePackSerializerOptions"/>.
    /// </summary>
    /// <param name="options">Custom MessagePack serializer options, or default contractless options if null.</param>
    public KyrolusMessagePackCacheSerializer(MessagePackSerializerOptions? options)
    {
        this.options = options ?? ContractlessStandardResolver.Options;
    }

    /// <summary>
    /// Creates a new serializer instance configured with built-in LZ4 block compression.
    /// </summary>
    /// <remarks>
    /// <b>Real-World Use Case:</b>
    /// When caching large JSON-like collections (e.g. 5,000 product rows), LZ4 block compression 
    /// shrinks the binary size even further with almost zero CPU overhead during serialization.
    /// </remarks>
    /// <returns>A configured <see cref="KyrolusMessagePackCacheSerializer"/> instance with LZ4 compression enabled.</returns>
    public static KyrolusMessagePackCacheSerializer CreateWithLz4Compression()
    {
        var opts = ContractlessStandardResolver.Options.WithCompression(MessagePackCompression.Lz4BlockArray);
        return new KyrolusMessagePackCacheSerializer(opts);
    }

    /// <summary>
    /// Serializes a C# object value into a compact MessagePack binary byte array.
    /// </summary>
    /// <typeparam name="T">The type of the object being serialized.</typeparam>
    /// <param name="value">The object instance to serialize.</param>
    /// <returns>A compact binary byte array.</returns>
    public byte[] Serialize<T>(T value)
    {
        if (value is null) return [];
        return MessagePackSerializer.Serialize(value, options);
    }

    /// <summary>
    /// Deserializes a MessagePack binary byte array back into a strongly-typed C# object instance.
    /// </summary>
    /// <typeparam name="T">The expected C# type.</typeparam>
    /// <param name="payload">The binary byte array read from cache.</param>
    /// <returns>The reconstructed object of type <typeparamref name="T"/>, or default if the payload is empty.</returns>
    public T? Deserialize<T>(byte[] payload)
    {
        if (payload is null || payload.Length == 0) return default;
        return MessagePackSerializer.Deserialize<T>(payload, options);
    }
}
