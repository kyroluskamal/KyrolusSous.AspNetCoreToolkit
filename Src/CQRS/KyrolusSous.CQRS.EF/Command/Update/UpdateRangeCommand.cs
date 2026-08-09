namespace KyrolusSous.CQRS.EF.Command.Update;

public class UpdateRangeCommand<TResponse>(IEnumerable<TResponse> entities, bool cacheable = false)
: CacheableRequest(cacheable), IKyrolusCommand<IEnumerable<TResponse>>
    where TResponse : notnull
{
    public IEnumerable<TResponse> Entities { get; set; } = entities;
}

