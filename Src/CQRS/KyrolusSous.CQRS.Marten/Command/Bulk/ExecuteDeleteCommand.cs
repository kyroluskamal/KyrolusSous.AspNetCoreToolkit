namespace KyrolusSous.CQRS.Marten.Command.Bulk;

/// <summary>
/// Bulk delete command executed via the Marten repository's DeleteWhereAsync.
/// <paramref name="filter"/> is required: a missing filter would otherwise silently affect every
/// document. Callers who genuinely want to delete every document must pass an explicit
/// <c>x =&gt; true</c> predicate.
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
    /// The delete predicate. Required — pass <c>x =&gt; true</c> explicitly to affect every
    /// document. Init-only so it can't be nulled out after construction via an object initializer.
    /// </summary>
    public Expression<Func<TResponse, bool>> Filter { get; init; } = filter ?? throw new ArgumentNullException(nameof(filter), "A delete filter is required; pass 'x => true' explicitly to delete every document.");
    // Currently a no-op for this provider - Marten's Include never produces a SQL JOIN the way EF's
    // split-query setting addresses, so there's nothing to toggle. Kept for shape-compatibility with
    // callers written against the EF command type; see KyrolusSous.CQRS.EF.Command.Bulk.ExecuteDeleteCommand.
    public bool? UseSplitQuery { get; set; } = useSplitQuery;
    public string? TenantId { get; set; }
}