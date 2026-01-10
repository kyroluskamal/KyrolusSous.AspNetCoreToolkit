namespace KyrolusSous.CQRS.EF.Query;

public class GetByKeyValuesQuery<TResponse, TKey>(object?[] keyValues, bool cacheable = false) : CacheableRequest(cacheable), IKyrolusQuery<TResponse?>
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public List<string>? IncludeProperties { get; set; }
    public Expression<Func<TResponse, object?>>[]? IncludeExpressions { get; set; }
    public bool? AsNoTracking { get; set; }
    public bool? UseSplitQuery { get; set; }
    public object?[] KeyValues { get; set; } = keyValues;
}
