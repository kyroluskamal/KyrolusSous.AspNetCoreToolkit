namespace KyrolusSous.CQRS.Marten.Command.Update;

public class UpdateCommand<TResponse>(
    TResponse entity,
    Guid? expectedVersion = null,
    string? tenantId = null,
    bool cacheable = false)
    : CacheableRequest(cacheable), IKyrolusCommand<TResponse>
    where TResponse : notnull
{
    public TResponse Entity { get; set; } = entity;
    public Guid? ExpectedVersion { get; set; } = expectedVersion;
    public string? TenantId { get; set; } = tenantId;
}

