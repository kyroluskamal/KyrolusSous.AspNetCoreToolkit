using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

namespace KyrolusSous.CQRS.Marten.Query;

/// <summary>
/// CQRS query that executes a Marten query specification.
/// </summary>
public class MartenSpecificationQuery<TEntity>(
    IQuerySpecification<TEntity> specification,
    string? tenantId = null,
    bool cacheable = false)
    : CacheableRequest(cacheable), ISpecificationQuery<TEntity, TEntity>
    where TEntity : class
{
    public IQuerySpecification<TEntity> Specification { get; } = specification ?? throw new ArgumentNullException(nameof(specification));
    public string? TenantId { get; } = tenantId;
}
