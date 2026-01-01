

using KyrolusSous.Mediator.Abstractions.Interfaces;

namespace KyrolusSous.CQRS.EF.Command.Remove;

public class RemoveByIdCommand<TResponse, TKey>(object?[]? keyValues, bool cacheable = false) :
 CacheableRequest(cacheable), IKyrolusCommand
 where TResponse : class
where TKey : IEquatable<TKey>
{
    public object?[]? KeyValues { get; set; } = keyValues;
}
