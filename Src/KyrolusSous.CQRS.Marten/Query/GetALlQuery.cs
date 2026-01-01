namespace KyrolusSous.CQRS.Marten.Query;

public class GetAllQuery<TResponse>(bool cacheable = false) : CacheableRequest(cacheable), IKyrolusQuery<IEnumerable<TResponse>>
    where TResponse : class
{
    public MartenQueryOptions<TResponse>? Options { get; set; }
}
