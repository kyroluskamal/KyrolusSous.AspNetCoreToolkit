
using KyrolusSous.SourceMediator.Interfaces;

namespace KyrolusSous.CQRS.Base.Command.Patch;

public class PatchCommand<TResponse, TKey>(object?[]? keyValues, Dictionary<string, object> updates, bool cacheable = false)
: CacheableRequest(cacheable), ICommand<TResponse>
    where TResponse : notnull
{
    public object?[]? KeyValues { get; set; } = keyValues;
    public Dictionary<string, object> Updates { get; set; } = updates;
}