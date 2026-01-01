

using KyrolusSous.Mediator.Abstractions.Interfaces;

namespace KyrolusSous.CQRS.EF.Command.Remove;

public class RemoveByEntityCommand<TResponse>(TResponse entity, bool cacheable = false)
: CacheableRequest(cacheable), IKyrolusCommand
    where TResponse : notnull
{
    public TResponse Entity { get; set; } = entity;

}
