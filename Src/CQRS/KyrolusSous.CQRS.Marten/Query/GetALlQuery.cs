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
    // Currently no-ops for this provider: Marten has no per-query equivalent of EF's
    // AsNoTracking()/AsSplitQuery() (no query-level tracking toggle, and Include never produces a
    // SQL JOIN the way EF's split-query setting addresses). Kept for shape-compatibility with
    // callers written against the EF query type; see KyrolusSous.CQRS.EF.Query.GetAllQuery.
    public bool? AsNoTracking { get; set; }
    public bool? UseSplitQuery { get; set; }
    public bool IncludeDeleted { get; set; }
    public bool DeletedOnly { get; set; }
    public Expression<Func<TResponse, TResponse>>? Selector { get; set; }
    public string? TenantId { get; set; }
}

