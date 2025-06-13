
using KyrolusSous.SourceMediator.Interfaces;

namespace KyrolusSous.CQRS.Base.Command.Add;

public class AddCommand<TResponse>(TResponse entity, bool cacheable = false)
: CacheableRequest(cacheable), ICommand<TResponse>
where TResponse : notnull
{
    public TResponse Entity { get; set; } = entity;
}

