using KyrolusSous.Mediator.Abstractions.Interfaces;

namespace KyrolusSous.CQRS.EF.Command.Update;

public class UpdateCommand<TResponse>(TResponse entity, bool cacheable = false)
: CacheableRequest(cacheable), IKyrolusCommand<TResponse>
    where TResponse : notnull
{
    public TResponse Entity { get; set; } = entity;
}

