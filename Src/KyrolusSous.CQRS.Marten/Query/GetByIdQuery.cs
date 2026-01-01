namespace KyrolusSous.CQRS.Marten.Query;

public class GetByIdQuery<TResponse, TKey>(TKey id, bool cacheable = false)
    : CacheableRequest(cacheable), IKyrolusQuery<MartenEntityResult<TResponse>?>
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public MartenQueryOptions<TResponse>? Options { get; set; }
    public TKey Id { get; set; } = id;
}
