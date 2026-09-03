namespace KyrolusSous.CQRS.EF.Command.Bulk;

/// <summary>
/// Bulk delete command executed via EF Core's ExecuteDeleteAsync. <paramref name="filter"/> is
/// required: a missing filter would otherwise silently affect every row in the table. Callers who
/// genuinely want to delete every row must pass an explicit <c>x =&gt; true</c> predicate.
/// </summary>
public sealed class ExecuteDeleteCommand<TResponse, TKey>(
    Expression<Func<TResponse, bool>> filter,
    bool cacheable = false,
    bool? useSplitQuery = null)
    : CacheableRequest(cacheable), IKyrolusCommand<int>
    where TResponse : class
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// The delete predicate. Required — pass <c>x =&gt; true</c> explicitly to affect every row.
    /// Init-only so it can't be nulled out after construction via an object initializer.
    /// </summary>
    public Expression<Func<TResponse, bool>> Filter { get; init; } = filter ?? throw new ArgumentNullException(nameof(filter), "A delete filter is required; pass 'x => true' explicitly to delete every row.");
    public bool? UseSplitQuery { get; set; } = useSplitQuery;
}
