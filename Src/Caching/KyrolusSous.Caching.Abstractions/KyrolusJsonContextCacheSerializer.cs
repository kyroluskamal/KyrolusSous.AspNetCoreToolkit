namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// A high-performance, 100% Native AOT-compatible cache serializer using compile-time source-generated <see cref="JsonSerializerContext"/>.
/// </summary>
/// <remarks>
/// Completely eliminates runtime reflection and IL generation, making it ideal for ahead-of-time compiled microservices, 
/// serverless functions (AWS Lambda / Azure Functions), and trimmed container images.
/// </remarks>
/// <param name="context">The pre-generated <see cref="JsonSerializerContext"/> instance containing metadata for cached types.</param>
public sealed class KyrolusJsonContextCacheSerializer(JsonSerializerContext context) : IKyrolusCacheSerializer
{
    private readonly JsonSerializerContext context = context ?? throw new ArgumentNullException(nameof(context));

    /// <summary>
    /// Serializes an object using compile-time type metadata from the registered <see cref="JsonSerializerContext"/>.
    /// </summary>
    /// <typeparam name="T">The type of the object being serialized.</typeparam>
    /// <param name="value">The object instance.</param>
    /// <returns>A UTF-8 JSON byte array generated without runtime reflection.</returns>
    public byte[] Serialize<T>(T value)
    {
        var typeInfo = GetTypeInfo<T>();
        return JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
    }

    /// <summary>
    /// Deserializes a UTF-8 JSON byte payload using compile-time type metadata.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="payload">The UTF-8 JSON byte array.</param>
    /// <returns>The deserialized object instance.</returns>
    public T? Deserialize<T>(byte[] payload)
    {
        if (payload is null || payload.Length == 0)
        {
            return default;
        }

        var typeInfo = GetTypeInfo<T>();
        return (T?)JsonSerializer.Deserialize(payload, typeInfo);
    }

    private JsonTypeInfo GetTypeInfo<T>()
    {
        return context.GetTypeInfo(typeof(T))
            ?? throw new InvalidOperationException($"No JsonTypeInfo registered in JsonSerializerContext for {typeof(T).FullName}. Ensure [JsonSerializable(typeof({typeof(T).Name}))] is declared.");
    }
}
