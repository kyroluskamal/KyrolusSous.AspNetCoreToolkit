namespace KyrolusSous.CQRS.Marten.Command.Bulk;

public sealed class ExecuteDeleteCommand<TResponse, TKey>(
    Expression<Func<TResponse, bool>>? filter,
    bool cacheable = false,
    bool? useSplitQuery = null)
    : CacheableRequest(cacheable), IKyrolusCommand<int>
    where TResponse : class
    where TKey : notnull, IEquatable<TKey>
{
    public Expression<Func<TResponse, bool>>? Filter { get; set; } = filter;
    public bool? UseSplitQuery { get; set; } = useSplitQuery;
}