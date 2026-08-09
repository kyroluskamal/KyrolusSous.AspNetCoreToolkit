using KyrolusSous.Repositories.Marten.Abstractions.Query;

namespace KyrolusSous.CQRS.Marten.Query;

public class GetByIdQuery<TResponse, TKey>(TKey id, bool cacheable = false)
    : CacheableRequest(cacheable), IKyrolusQuery<TResponse?>
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public TKey Id { get; set; } = id;
    public List<string>? IncludeProperties { get; set; }
    public Expression<Func<TResponse, object?>>[]? IncludeExpressions { get; set; }
    public IncludeGraph<TResponse>? IncludeGraph { get; set; }
    public bool? AsNoTracking { get; set; }
    public bool? UseSplitQuery { get; set; }
    public bool IncludeDeleted { get; set; }
    public string? RowVersionPropertyName { get; set; }
    public string? TenantId { get; set; }
}

