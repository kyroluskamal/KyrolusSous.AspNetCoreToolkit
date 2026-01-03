using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace KyrolusSous.Caching.Abstractions;

public sealed class KyrolusJsonContextCacheSerializer(JsonSerializerContext context) : IKyrolusCacheSerializer
{
    private readonly JsonSerializerContext context = context ?? throw new ArgumentNullException(nameof(context));

    public byte[] Serialize<T>(T value)
    {
        var typeInfo = GetTypeInfo<T>();
        return JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
    }

    public T? Deserialize<T>(byte[] payload)
    {
        var typeInfo = GetTypeInfo<T>();
        return (T?)JsonSerializer.Deserialize(payload, typeInfo);
    }

    private JsonTypeInfo GetTypeInfo<T>()
    {
        return context.GetTypeInfo(typeof(T))
            ?? throw new InvalidOperationException($"No JsonTypeInfo registered for {typeof(T).FullName}.");
    }
}
