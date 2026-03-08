using KyrolusSous.Repositories.Marten.Abstractions.Query;

namespace KyrolusSous.CQRS.Marten.Query;

public class GetByKeyValuesQuery<TResponse, TKey>(object?[] keyValues, bool cacheable = false) : CacheableRequest(cacheable), IKyrolusQuery<TResponse?>
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public List<string>? IncludeProperties { get; set; }
    public Expression<Func<TResponse, object?>>[]? IncludeExpressions { get; set; }
    public IncludeGraph<TResponse>? IncludeGraph { get; set; }
    public bool? AsNoTracking { get; set; }
    public bool? UseSplitQuery { get; set; }
    public object?[] KeyValues { get; set; } = keyValues;
    public IReadOnlyList<string>? KeyPropertyNames { get; set; }
    public bool IncludeDeleted { get; set; }
    public string? TenantId { get; set; }
    public string? RowVersionPropertyName { get; set; }
}

