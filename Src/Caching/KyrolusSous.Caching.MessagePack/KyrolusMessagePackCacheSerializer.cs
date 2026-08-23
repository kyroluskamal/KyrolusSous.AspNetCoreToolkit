using KyrolusSous.Caching.Abstractions;
using MessagePack;
using MessagePack.Resolvers;

namespace KyrolusSous.Caching.MessagePack;

/// <summary>
/// High-performance MessagePack serializer implementing <see cref="IKyrolusCacheSerializer"/>.
/// Uses contractless standard resolver by default for seamless POCO serialization.
/// </summary>
public sealed class KyrolusMessagePackCacheSerializer : IKyrolusCacheSerializer
{
    private readonly MessagePackSerializerOptions options;

    /// <summary>
    /// Initializes a new instance with default contractless resolver options.
    /// </summary>
    public KyrolusMessagePackCacheSerializer()
        : this(ContractlessStandardResolver.Options)
    {
    }

    /// <summary>
    /// Initializes a new instance with custom <see cref="MessagePackSerializerOptions"/>.
    /// </summary>
    /// <param name="options">Custom MessagePack serializer options.</param>
    public KyrolusMessagePackCacheSerializer(MessagePackSerializerOptions options)
    {
        this.options = options ?? ContractlessStandardResolver.Options;
    }

    /// <summary>
    /// Creates a serializer instance with LZ4 compression enabled.
    /// </summary>
    public static KyrolusMessagePackCacheSerializer CreateWithLz4Compression()
    {
        var opts = ContractlessStandardResolver.Options.WithCompression(MessagePackCompression.Lz4BlockArray);
        return new KyrolusMessagePackCacheSerializer(opts);
    }

    public byte[] Serialize<T>(T value)
    {
        if (value is null) return [];
        return MessagePackSerializer.Serialize(value, options);
    }

    public T? Deserialize<T>(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0) return default;
        return MessagePackSerializer.Deserialize<T>(bytes, options);
    }
}
