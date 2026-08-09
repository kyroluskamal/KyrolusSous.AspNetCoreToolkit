using System.Text.Json;

namespace KyrolusSous.Caching.Abstractions;

public sealed class KyrolusJsonCacheSerializer(JsonSerializerOptions? options = null) : IKyrolusCacheSerializer
{
    private readonly JsonSerializerOptions options = options ?? new JsonSerializerOptions(JsonSerializerDefaults.General);

    public byte[] Serialize<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, options);

    public T? Deserialize<T>(byte[] payload) =>
        JsonSerializer.Deserialize<T>(payload, options);
}
