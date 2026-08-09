using KyrolusSous.Repositories.Marten.Abstractions.Query;

namespace KyrolusSous.CQRS.Marten.Query;

public class GetAllQuery<TResponse>(bool cacheable = false) : CacheableRequest(cacheable), IKyrolusQuery<IEnumerable<TResponse>>
    where TResponse : class
{
    public Expression<Func<TResponse, bool>>? Filter { get; set; }
    public Func<IQueryable<TResponse>, IOrderedQueryable<TResponse>>? OrderBy { get; set; }
    public List<string>? IncludeProperties { get; set; }
    public Expression<Func<TResponse, object?>>[]? IncludeExpressions { get; set; }
    public IncludeGraph<TResponse>? IncludeGraph { get; set; }
    public bool? AsNoTracking { get; set; }
    public bool? UseSplitQuery { get; set; }
    public bool IncludeDeleted { get; set; }
    public bool DeletedOnly { get; set; }
    public Expression<Func<TResponse, TResponse>>? Selector { get; set; }
    public string? TenantId { get; set; }
}

