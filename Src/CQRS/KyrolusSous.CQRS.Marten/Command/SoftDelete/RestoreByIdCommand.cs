namespace KyrolusSous.CQRS.Marten.Command.SoftDelete;

public sealed class RestoreByIdCommand<TResponse, TKey>(object?[]? keyValues, bool cacheable = false, string? tenantId = null)
    : CacheableRequest(cacheable), IKyrolusCommand<bool>
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public object?[]? KeyValues { get; set; } = keyValues;
    public string? TenantId { get; set; } = tenantId;
}
