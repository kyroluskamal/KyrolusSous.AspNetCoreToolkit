    namespace KyrolusSous.CQRS.Marten.Query;

/// <summary>
/// CQRS query that executes a Marten query specification.
/// </summary>
public class MartenSpecificationQuery<TEntity>(
    IKyrolusQuerySpecification<TEntity> specification,
    string? tenantId = null,
    bool cacheable = false)
    : CacheableRequest(cacheable), IKyrolusSpecificationQuery<TEntity, TEntity>
    where TEntity : class
{
    public IKyrolusQuerySpecification<TEntity> Specification { get; } = specification ?? throw new ArgumentNullException(nameof(specification));
    public string? TenantId { get; } = tenantId;
}
