    namespace KyrolusSous.CQRS.Marten.Query;

/// <summary>
/// CQRS query that executes a Marten query specification, optionally projecting each matched
/// document to <typeparamref name="TResult"/>.
/// </summary>
/// <remarks>
/// Marten's <see cref="IKyrolusQuerySpecification{TEntity}"/> only knows how to transform an
/// <c>IMartenQueryable&lt;TEntity&gt;</c> - unlike EF's <c>IKyrolusQuerySpecification&lt;TEntity, TResult&gt;</c>,
/// it carries no projection of its own, so it stays a single-generic contract here. <see cref="Selector"/>
/// therefore lives on the query, the same way <see cref="GetAllQuery{TResponse}.Selector"/> and
/// <see cref="GetPagedQuery{TResponse, TKey}.Selector"/> already carry projection separately from the
/// filtering/ordering concerns for this provider. Passing <typeparamref name="TResult"/> equal to
/// <typeparamref name="TEntity"/> with no <see cref="Selector"/> reproduces the pre-projection behavior
/// of this type exactly.
/// </remarks>
/// <typeparam name="TEntity">The entity type the specification filters and orders.</typeparam>
/// <typeparam name="TResult">The returned projection or entity type.</typeparam>
public class MartenSpecificationQuery<TEntity, TResult>(
    IKyrolusQuerySpecification<TEntity> specification,
    Expression<Func<TEntity, TResult>>? selector = null,
    string? tenantId = null,
    bool cacheable = false)
    : CacheableRequest(cacheable), IKyrolusSpecificationQuery<TEntity, TResult>
    where TEntity : class
    where TResult : notnull
{
    public IKyrolusQuerySpecification<TEntity> Specification { get; } = specification ?? throw new ArgumentNullException(nameof(specification));
    public Expression<Func<TEntity, TResult>>? Selector { get; } = selector;
    public string? TenantId { get; } = tenantId;
}
