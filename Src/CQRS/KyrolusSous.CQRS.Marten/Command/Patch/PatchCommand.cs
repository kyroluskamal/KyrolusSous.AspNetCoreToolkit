
namespace KyrolusSous.CQRS.Marten.Command.Patch;

public class PatchCommand<TResponse, TKey>(
    TKey id,
    Dictionary<string, object> updates,
    string? tenantId = null,
    bool cacheable = false)
    : CacheableRequest(cacheable), IKyrolusCommand<TResponse?>, IKyrolusPropertyUpdateRequest
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public TKey Id { get; set; } = id;
    public Dictionary<string, object> Updates { get; set; } = updates;
    public string? TenantId { get; set; } = tenantId;
    public string? RowVersionPropertyName { get; set; }

    /// <inheritdoc cref="IKyrolusPropertyUpdateRequest.AllowedProperties"/>
    public IReadOnlySet<string>? AllowedProperties { get; set; }

    IEnumerable<string> IKyrolusPropertyUpdateRequest.UpdatedPropertyNames => Updates.Keys;
}
