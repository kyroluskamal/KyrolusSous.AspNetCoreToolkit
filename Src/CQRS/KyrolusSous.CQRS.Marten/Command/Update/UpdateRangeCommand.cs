namespace KyrolusSous.CQRS.Marten.Command.Update;

public class UpdateRangeCommand<TResponse>(
    IEnumerable<TResponse> entities,
    string? tenantId = null,
    bool cacheable = false)
    : CacheableRequest(cacheable), IKyrolusCommand<IEnumerable<TResponse>>
    where TResponse : notnull
{
    public IEnumerable<TResponse> Entities { get; set; } = entities;
    public string? TenantId { get; set; } = tenantId;
}

