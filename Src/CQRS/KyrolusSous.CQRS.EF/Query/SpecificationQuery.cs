using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;

namespace KyrolusSous.CQRS.EF.Query;

/// <summary>
/// CQRS query that executes an EF Core query specification.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TResult">The projected result type.</typeparam>
public class SpecificationQuery<TEntity, TResult>(
    IKyrolusQuerySpecification<TEntity, TResult> specification,
    bool cacheable = false)
    : CacheableRequest(cacheable), IKyrolusSpecificationQuery<TEntity, TResult>
    where TEntity : class
{
    public IKyrolusQuerySpecification<TEntity, TResult> Specification { get; } = specification ?? throw new ArgumentNullException(nameof(specification));
}
