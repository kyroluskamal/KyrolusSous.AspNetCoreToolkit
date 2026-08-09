namespace KyrolusSous.Caching.Abstractions;

public interface IKyrolusCachePayloadTransformer
{
    byte[] Transform(byte[] payload);
    byte[] Restore(byte[] payload);
}

public interface IKyrolusOrderedCachePayloadTransformer : IKyrolusCachePayloadTransformer
{
    int Order { get; }
}
