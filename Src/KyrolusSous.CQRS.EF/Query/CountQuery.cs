namespace KyrolusSous.CQRS.EF.Query;

public class CountQuery<TResponse>(bool cacheable = false) : CacheableRequest(cacheable), IKyrolusQuery<long>
    where TResponse : class
{
    public Expression<Func<TResponse, bool>>? Filter { get; set; }
    public bool IncludeDeleted { get; set; }
}
