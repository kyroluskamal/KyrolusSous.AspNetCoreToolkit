namespace KyrolusSous.CQRS.EF.Query;

public class GetAllQuery<TResponse>(bool cacheable = false) : CacheableRequest(cacheable), IKyrolusQuery<IEnumerable<TResponse>>
    where TResponse : class
{
    public Expression<Func<TResponse, bool>>? Filter { get; set; }
    public Func<IQueryable<TResponse>, IOrderedQueryable<TResponse>>? OrderBy { get; set; }
    public List<string>? IncludeProperties { get; set; }

}
