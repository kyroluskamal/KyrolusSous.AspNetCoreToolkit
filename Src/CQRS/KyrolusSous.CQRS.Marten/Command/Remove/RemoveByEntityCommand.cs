

namespace KyrolusSous.CQRS.Marten.Command.Remove;

public class RemoveByEntityCommand<TResponse>(
    TResponse entity,
    Guid? expectedVersion = null,
    string? tenantId = null,
    bool cacheable = false)
    : CacheableRequest(cacheable), IKyrolusCommand
    where TResponse : notnull
{
    public TResponse Entity { get; set; } = entity;
    public Guid? ExpectedVersion { get; set; } = expectedVersion;
    public string? TenantId { get; set; } = tenantId;
}
