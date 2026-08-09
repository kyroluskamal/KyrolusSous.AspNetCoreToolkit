
namespace KyrolusSous.CQRS.EF.Command.Add;

public class AddCommand<TResponse>(TResponse entity, bool cacheable = false)
: CacheableRequest(cacheable), IKyrolusCommand<TResponse>
where TResponse : notnull
{
    public TResponse Entity { get; set; } = entity;
}

