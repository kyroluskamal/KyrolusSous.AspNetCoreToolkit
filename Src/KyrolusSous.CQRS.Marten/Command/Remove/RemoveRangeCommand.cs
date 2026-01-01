

namespace KyrolusSous.CQRS.Marten.Command.Remove;

public class RemoveRangeCommand<TResponse>(
    IEnumerable<TResponse> entities,
    string? tenantId = null,
    bool cacheable = false)
    : CacheableRequest(cacheable), IKyrolusCommand
    where TResponse : notnull
{
    public IEnumerable<TResponse> Entities { get; set; } = entities;
    public string? TenantId { get; set; } = tenantId;
}
