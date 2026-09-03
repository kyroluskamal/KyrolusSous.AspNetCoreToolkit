namespace KyrolusSous.CQRS.Marten.Command.Bulk;

/// <summary>
/// Bulk update command executed via the Marten repository's PatchWhereAsync.
/// <paramref name="filter"/> is required: a missing filter would otherwise silently affect every
/// document. Callers who genuinely want to update every document must pass an explicit
/// <c>x =&gt; true</c> predicate.
/// </summary>
public sealed class ExecuteUpdateCommand<TResponse, TKey>(
    Expression<Func<TResponse, bool>> filter,
    Dictionary<string, object> updates,
    bool cacheable = false,
    bool? useSplitQuery = null)
    : CacheableRequest(cacheable), IKyrolusCommand<int>, IKyrolusPropertyUpdateRequest
    where TResponse : class
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// The update predicate. Required — pass <c>x =&gt; true</c> explicitly to affect every
    /// document. Init-only so it can't be nulled out after construction via an object initializer.
    /// </summary>
    public Expression<Func<TResponse, bool>> Filter { get; init; } = filter ?? throw new ArgumentNullException(nameof(filter), "An update filter is required; pass 'x => true' explicitly to update every document.");
    public Dictionary<string, object> Updates { get; set; } = updates;
    public bool? UseSplitQuery { get; set; } = useSplitQuery;
    public string? TenantId { get; set; }

    /// <inheritdoc cref="IKyrolusPropertyUpdateRequest.AllowedProperties"/>
    public IReadOnlySet<string>? AllowedProperties { get; set; }

    IEnumerable<string> IKyrolusPropertyUpdateRequest.UpdatedPropertyNames => Updates.Keys;
}
