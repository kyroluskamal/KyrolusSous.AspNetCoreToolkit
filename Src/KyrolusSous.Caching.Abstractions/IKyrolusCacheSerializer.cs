namespace KyrolusSous.Caching.Abstractions;

public interface IKyrolusCacheSerializer
{
    byte[] Serialize<T>(T value);
    T? Deserialize<T>(byte[] payload);
}
