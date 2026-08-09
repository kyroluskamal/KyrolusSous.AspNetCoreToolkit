namespace KyrolusSous.CQRS.Marten.Command.Bulk;

public sealed class BulkUpsertCommand<TResponse, TKey>(
    IReadOnlyList<TResponse> entities,
    IReadOnlyList<string> keyPropertyNames,
    bool cacheable = false)
    : CacheableRequest(cacheable), IKyrolusCommand<IEnumerable<TResponse>>
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public IReadOnlyList<TResponse> Entities { get; set; } = entities;
    public IReadOnlyList<string> KeyPropertyNames { get; set; } = keyPropertyNames;
    public string? TenantId { get; set; }
}
