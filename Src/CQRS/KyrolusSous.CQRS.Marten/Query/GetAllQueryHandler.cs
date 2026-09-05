
using KyrolusSous.Repositories.Marten.Abstractions.Query;

namespace KyrolusSous.CQRS.Marten.Query;

public class GetAllQueryHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
: IKyrolusQueryHandler<GetAllQuery<TResponse>, IEnumerable<TResponse>>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<IEnumerable<TResponse>> Handle(GetAllQuery<TResponse> query, CancellationToken cancellationToken)
    {
        var options = BuildOptions(query);
        if (query.DeletedOnly)
        {
            var soft = TryResolveSoftRepository();
            if (soft is not null)
            {
                ThrowIfProjectionUnsupported(query);
                return await soft.GetDeletedOnlyAsync(options, cancellationToken).ConfigureAwait(false);
            }
        }

        if (query.IncludeDeleted)
        {
            var soft = TryResolveSoftRepository();
            if (soft is not null)
            {
                ThrowIfProjectionUnsupported(query);
                return await soft.GetAllIncludingDeletedAsync(options, cancellationToken).ConfigureAwait(false);
            }
        }

        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        if (query.Selector is not null)
        {
            return await repo.QueryAsync<TResponse>(options, q => (global::Marten.Linq.IMartenQueryable<TResponse>)q.Select(query.Selector), cancellationToken).ConfigureAwait(false);
        }

        return await repo.GetAllAsync(options, cancellationToken).ConfigureAwait(false);
    }

    private static MartenQueryOptions<TResponse> BuildOptions(GetAllQuery<TResponse> query)
    {
        var mergedExpressions = MergeIncludeExpressions(query.IncludeExpressions, query.IncludeGraph);
        return new MartenQueryOptions<TResponse>(
            Filter: query.Filter,
            OrderBy: query.OrderBy,
            IncludeProperties: query.IncludeProperties,
            IncludeExpressions: mergedExpressions,
            TenantId: query.TenantId,
            IncludeSoftDeleted: query.IncludeDeleted || query.DeletedOnly);
    }

    private static Expression<Func<TResponse, object?>>[]? MergeIncludeExpressions(
        Expression<Func<TResponse, object?>>[]? includes,
        IncludeGraph<TResponse>? graph)
    {
        if (includes is null && (graph?.Includes?.Count ?? 0) == 0) return null;
        var merged = new List<Expression<Func<TResponse, object?>>>();
        if (includes is not null) merged.AddRange(includes);
        if (graph?.Includes is not null) merged.AddRange(graph.Includes);
        return merged.Count == 0 ? null : merged.ToArray();
    }

    private IKyrolusMartenSoftDeleteRepositoryAsync<TSession, TResponse, TKey>? TryResolveSoftRepository()
    {
        try
        {
            return unitOfWork.GetRepository<IKyrolusMartenSoftDeleteRepositoryAsync<TSession, TResponse, TKey>>();
        }
        catch (InvalidOperationException ex) when (ex.IsRepositoryNotRegistered())
        {
            return null;
        }
    }

    /// <remarks>
    /// Neither <see cref="IKyrolusMartenSoftDeleteRepositoryAsync{TSession, TEntity, TKey}.GetAllIncludingDeletedAsync"/>
    /// nor <c>GetDeletedOnlyAsync</c> has a projected overload, so a caller's <see cref="GetAllQuery{TResponse}.Selector"/>
    /// (potentially used to redact PII) would otherwise be silently dropped and full, un-projected
    /// entities returned instead - a silent-wrong-data outcome. Failing loudly here instead matches
    /// this codebase's "fail closed" precedent for a request the current code path cannot honor.
    /// </remarks>
    private static void ThrowIfProjectionUnsupported(GetAllQuery<TResponse> query)
    {
        if (query.Selector is not null)
        {
            throw new InvalidOperationException(
                "[Kyrolus CQRS] GetAllQuery.Selector is not supported when browsing soft-deleted " +
                "records (IncludeDeleted/DeletedOnly) - drop the projection, or query without " +
                "IncludeDeleted/DeletedOnly.");
        }
    }
}

