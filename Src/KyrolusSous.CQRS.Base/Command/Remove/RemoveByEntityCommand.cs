

using KyrolusSous.SourceMediator.Interfaces;

namespace KyrolusSous.CQRS.Base.Command.Remove;

public class RemoveByEntityCommand<TResponse>(TResponse entity, bool cacheable = false)
: CacheableRequest(cacheable), ICommand
    where TResponse : notnull
{
    public TResponse Entity { get; set; } = entity;

}
