namespace KyrolusSous.CQRS.Marten.Query;

/// <summary>
/// Handles <see cref="MartenSpecificationQuery{TEntity, TResult}"/> by executing the specification
/// against the Marten repository, projecting through <c>query.Selector</c> when one is supplied.
/// </summary>
/// <remarks>
/// Mirrors the <c>query.Selector</c> routing already used by <see cref="GetAllQueryHandler{TSession, TResponse, TKey}"/>
/// and <see cref="GetPagedQueryHandler{TSession, TResponse, TKey}"/>: <see cref="IKyrolusMartenRepositoryAsync{TSession, TEntity, TKey}.QueryAsync{TProjection}"/>
/// applies the specification (and any filter/order) to the base queryable before the projection runs,
/// so the projected shape never bypasses the specification's own filtering.
/// </remarks>
public class MartenSpecificationQueryHandler<TSession, TEntity, TResult, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusQueryHandler<MartenSpecificationQuery<TEntity, TResult>, IReadOnlyList<TResult>>
    where TSession : class, IDocumentSession
    where TEntity : class
    where TResult : notnull
    where TKey : IEquatable<TKey>
{
    private readonly IKyrolusMartenUnitOfWork<TSession> _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

    public async Task<IReadOnlyList<TResult>> Handle(MartenSpecificationQuery<TEntity, TResult> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var repo = _unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TEntity, TKey>>();
        var options = new MartenQueryOptions<TEntity>(
            TenantId: query.TenantId,
            Specification: query.Specification);

        if (query.Selector is not null)
        {
            var projected = await repo.QueryAsync<TResult>(
                options,
                q => (global::Marten.Linq.IMartenQueryable<TResult>)q.Select(query.Selector),
                cancellationToken).ConfigureAwait(false);
            return projected is IReadOnlyList<TResult> projectedList ? projectedList : projected.ToList();
        }

        // No Selector means the caller wants matched documents back as-is, which is only meaningful
        // when TResult is TEntity itself - reproducing this type's pre-projection behavior exactly.
        // Anything else has no way to reach TResult and would otherwise silently hand back the wrong
        // shape (or fail an unsafe cast), so this fails closed instead, matching the precedent set by
        // GetAllQueryHandler.ThrowIfProjectionUnsupported for an unsatisfiable projection request.
        if (typeof(TResult) != typeof(TEntity))
        {
            throw new InvalidOperationException(
                $"[Kyrolus CQRS] MartenSpecificationQuery<{typeof(TEntity).Name}, {typeof(TResult).Name}> requires a " +
                "Selector when TResult differs from TEntity - without one there is no way to project the matched documents.");
        }

        var results = await repo.GetAllAsync(options, cancellationToken).ConfigureAwait(false);
        var entities = results.Cast<TResult>();
        return entities is IReadOnlyList<TResult> list ? list : entities.ToList();
    }
}
