

namespace KyrolusSous.CQRS.Marten.Command.Remove;

public class RemoveByIdCommand<TResponse, TKey>(
    TKey id,
    Guid? expectedVersion = null,
    string? tenantId = null,
    bool cacheable = false) :
    CacheableRequest(cacheable), IKyrolusCommand
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public TKey Id { get; set; } = id;
    public Guid? ExpectedVersion { get; set; } = expectedVersion;
    public string? TenantId { get; set; } = tenantId;
}
