

using KyrolusSous.Mediator.Abstractions.Interfaces;

namespace KyrolusSous.CQRS.EF.Command.Remove;

public class RemoveRangeCommand<TResponse>(IEnumerable<TResponse> entities, bool cacheable = false)
: CacheableRequest(cacheable), IKyrolusCommand
    where TResponse : notnull
{
    public IEnumerable<TResponse> Entities { get; set; } = entities;
}
