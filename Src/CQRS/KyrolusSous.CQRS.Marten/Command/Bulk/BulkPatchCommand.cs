namespace KyrolusSous.CQRS.Marten.Command.Bulk;

public sealed record KyrolusBulkPatchItem(
    object?[] KeyValues,
    Dictionary<string, object> Updates);

public sealed class BulkPatchCommand<TResponse, TKey>(
    IReadOnlyList<KyrolusBulkPatchItem> items,
    bool cacheable = false)
    : CacheableRequest(cacheable), IKyrolusCommand<int>, IKyrolusPropertyUpdateRequest
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public IReadOnlyList<KyrolusBulkPatchItem> Items { get; set; } = items;
    public IReadOnlyList<string>? KeyPropertyNames { get; set; }
    public string? TenantId { get; set; }

    /// <inheritdoc cref="IKyrolusPropertyUpdateRequest.AllowedProperties"/>
    public IReadOnlySet<string>? AllowedProperties { get; set; }

    IEnumerable<string> IKyrolusPropertyUpdateRequest.UpdatedPropertyNames
        => Items.SelectMany(static i => i.Updates.Keys);
}
