namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Defines the serializer contract responsible for converting strongly-typed C# objects 
/// into raw byte arrays and reconstructing them upon cache reads.
/// </summary>
public interface IKyrolusCacheSerializer
{
    /// <summary>
    /// Serializes a strongly-typed C# object value into a byte array (e.g. UTF-8 JSON or binary MessagePack).
    /// </summary>
    /// <typeparam name="T">The type of the object being serialized.</typeparam>
    /// <param name="value">The object instance to serialize.</param>
    /// <returns>A byte array representing the serialized object.</returns>
    byte[] Serialize<T>(T value);

    /// <summary>
    /// Deserializes a byte array back into a strongly-typed C# object instance.
    /// </summary>
    /// <typeparam name="T">The expected type of the deserialized object.</typeparam>
    /// <param name="payload">The raw serialized byte array.</param>
    /// <returns>The reconstructed object of type <typeparamref name="T"/>, or <c>null</c> if the payload represents null or empty data.</returns>
    T? Deserialize<T>(byte[] payload);
}
