using KyrolusSous.CQRS.Abstractions.Models;
using KyrolusSous.Repositories.Marten.Abstractions.Query;

namespace KyrolusSous.CQRS.Marten.Query;

public sealed class GetSeekQuery<TResponse, TKey>(int pageSize, string? cursor = null, bool cacheable = false)
    : CacheableRequest(cacheable), IKyrolusQuery<KyrolusSeekResult<TResponse>>
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public Expression<Func<TResponse, bool>>? Filter { get; set; }
    public List<string>? IncludeProperties { get; set; }
    public Expression<Func<TResponse, object?>>[]? IncludeExpressions { get; set; }
    public IncludeGraph<TResponse>? IncludeGraph { get; set; }
    // Currently no-ops for this provider - see the remark on GetAllQuery<TResponse>.AsNoTracking.
    public bool? AsNoTracking { get; set; }
    public bool? UseSplitQuery { get; set; }
    public int PageSize { get; set; } = pageSize;
    public string? Cursor { get; set; } = cursor;
    public bool IncludeDeleted { get; set; }
    public bool IncludeTotalCount { get; set; }
    public bool Descending { get; set; }
    public IReadOnlyList<string>? SeekPropertyNames { get; set; }
    public Expression<Func<TResponse, TResponse>>? Selector { get; set; }
    public string? TenantId { get; set; }
}

