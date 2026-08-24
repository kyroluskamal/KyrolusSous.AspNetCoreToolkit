namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Standard cache serializer implementation using <see cref="System.Text.Json.JsonSerializer"/> for UTF-8 JSON serialization.
/// </summary>
/// <param name="options">Optional custom <see cref="JsonSerializerOptions"/>. If <c>null</c>, uses <see cref="JsonSerializerDefaults.General"/>.</param>
public sealed class KyrolusJsonCacheSerializer(JsonSerializerOptions? options = null) : IKyrolusCacheSerializer
{
    private readonly JsonSerializerOptions options = options ?? new JsonSerializerOptions(JsonSerializerDefaults.General);

    /// <summary>
    /// Serializes an object value directly to UTF-8 JSON bytes.
    /// </summary>
    /// <typeparam name="T">The type of the object being serialized.</typeparam>
    /// <param name="value">The object value.</param>
    /// <returns>A UTF-8 encoded JSON byte array.</returns>
    public byte[] Serialize<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, options);

    /// <summary>
    /// Deserializes UTF-8 JSON bytes back into a C# object of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="payload">The UTF-8 JSON byte array.</param>
    /// <returns>The deserialized object instance.</returns>
    public T? Deserialize<T>(byte[] payload) =>
        payload is null || payload.Length == 0 ? default : JsonSerializer.Deserialize<T>(payload, options);
}
