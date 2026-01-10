using KyrolusSous.CQRS.Abstractions.Models;

namespace KyrolusSous.CQRS.EF.Query;

public sealed class GetPagedQuery<TResponse, TKey>(int pageNumber, int pageSize, bool cacheable = false)
    : CacheableRequest(cacheable), IKyrolusQuery<KyrolusPagedResult<TResponse>>
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public Expression<Func<TResponse, bool>>? Filter { get; set; }
    public Func<IQueryable<TResponse>, IOrderedQueryable<TResponse>>? OrderBy { get; set; }
    public List<string>? IncludeProperties { get; set; }
    public Expression<Func<TResponse, object?>>[]? IncludeExpressions { get; set; }
    public bool? AsNoTracking { get; set; }
    public bool? UseSplitQuery { get; set; }
    public int PageNumber { get; set; } = pageNumber;
    public int PageSize { get; set; } = pageSize;
}
