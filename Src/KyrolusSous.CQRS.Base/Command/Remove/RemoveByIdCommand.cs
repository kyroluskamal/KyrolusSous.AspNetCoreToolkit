

using KyrolusSous.SourceMediator.Interfaces;

namespace KyrolusSous.CQRS.Base.Command.Remove;

public class RemoveByIdCommand<TResponse, TKey>(object?[]? keyValues, bool cacheable = false) :
 CacheableRequest(cacheable), ICommand
 where TResponse : class
where TKey : IEquatable<TKey>
{
    public object?[]? KeyValues { get; set; } = keyValues;
}
