

using KyrolusSous.SourceMediator.Interfaces;

namespace KyrolusSous.CQRS.Base.Command.Remove;

public class RemoveRangeCommand<TResponse>(IEnumerable<TResponse> entities, bool cacheable = false)
: CacheableRequest(cacheable), ICommand
    where TResponse : notnull
{
    public IEnumerable<TResponse> Entities { get; set; } = entities;
}
