namespace KyrolusSous.CQRS.EF.Command.SoftDelete;

public sealed class SoftDeleteByIdCommand<TResponse, TKey>(object?[]? keyValues, bool cacheable = false)
    : CacheableRequest(cacheable), IKyrolusCommand<bool>
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public object?[]? KeyValues { get; set; } = keyValues;
}
