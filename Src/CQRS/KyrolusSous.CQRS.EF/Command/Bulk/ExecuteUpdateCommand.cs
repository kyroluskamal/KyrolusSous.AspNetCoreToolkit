using KyrolusSous.Mediator.Abstractions.Interfaces;

namespace KyrolusSous.CQRS.EF.Command.Bulk;

public sealed class ExecuteUpdateCommand<TResponse, TKey>(
    Expression<Func<TResponse, bool>>? filter,
    Dictionary<string, object> updates,
    bool cacheable = false,
    bool? useSplitQuery = null)
    : CacheableRequest(cacheable), IKyrolusCommand<int>
    where TResponse : class
    where TKey : notnull, IEquatable<TKey>
{
    public Expression<Func<TResponse, bool>>? Filter { get; set; } = filter;
    public Dictionary<string, object> Updates { get; set; } = updates;
    public bool? UseSplitQuery { get; set; } = useSplitQuery;
}
