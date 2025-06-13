using KyrolusSous.SourceMediator.Interfaces;

namespace KyrolusSous.CQRS.Base.Command.Update;

public class UpdateRangeCommand<TResponse>(IEnumerable<TResponse> entities, bool cacheable = false)
: CacheableRequest(cacheable), ICommand<IEnumerable<TResponse>>
    where TResponse : notnull
{
    public IEnumerable<TResponse> Entities { get; set; } = entities;
}

