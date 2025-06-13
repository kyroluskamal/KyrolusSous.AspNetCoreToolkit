using KyrolusSous.SourceMediator.Interfaces;

namespace KyrolusSous.CQRS.Base.Command.Add;

public class AddRangeCommand<TResponse>(IEnumerable<TResponse> entities, bool cacheable = false)
: CacheableRequest(cacheable), ICommand<IEnumerable<TResponse>>
        where TResponse : class
{
    public IEnumerable<TResponse> Entities { get; set; } = entities;
}
