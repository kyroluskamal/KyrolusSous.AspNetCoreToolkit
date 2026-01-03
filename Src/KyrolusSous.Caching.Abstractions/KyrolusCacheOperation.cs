namespace KyrolusSous.Caching.Abstractions;

public enum KyrolusCacheOperation
{
    Get,
    GetMany,
    Set,
    SetMany,
    Remove,
    RemoveMany,
    RemoveByTag,
    RemoveByPattern,
    Exists,
    GetOrCreate
}
