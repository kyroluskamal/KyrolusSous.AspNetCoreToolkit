using KyrolusSous.CQRS.Abstractions.Models;
using KyrolusSous.Repositories.Marten.Abstractions.Query;

namespace KyrolusSous.CQRS.Marten.Query;

public sealed class GetPagedQueryHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusQueryHandler<GetPagedQuery<TResponse, TKey>, KyrolusPagedResult<TResponse>>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<KyrolusPagedResult<TResponse>> Handle(GetPagedQuery<TResponse, TKey> query, CancellationToken cancellationToken)
    {
        // Clamp caller-supplied paging so PageSize = int.MaxValue (or negative) can't force the
        // database to attempt to materialize an enormous or malformed result set.
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, KyrolusPagingLimits.MaxPageSize);

        if (query.IncludeDeleted || query.DeletedOnly)
        {
            var soft = TryResolveSoftRepository();
            if (soft is not null)
            {
                return await LoadIncludingDeletedAsync(soft, query, pageNumber, pageSize, cancellationToken).ConfigureAwait(false);
            }
        }

        var options = BuildOptions(query);
        var page = new MartenPageRequest(pageNumber, pageSize);
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        if (query.Selector is not null)
        {
            var projected = await repo.QueryPageAsync<TResponse>(options, q => (global::Marten.Linq.IMartenQueryable<TResponse>)q.Select(query.Selector), page, cancellationToken).ConfigureAwait(false);
            return new KyrolusPagedResult<TResponse>(projected.Items.ToList(), (int)projected.TotalCount, projected.PageNumber, projected.PageSize);
        }

        var result = await repo.GetPageAsync(options, page, cancellationToken).ConfigureAwait(false);
        return new KyrolusPagedResult<TResponse>(result.Items.ToList(), (int)result.TotalCount, result.PageNumber, result.PageSize);
    }

    /// <remarks>
    /// Neither <see cref="IKyrolusMartenSoftDeleteRepositoryAsync{TSession, TEntity, TKey}.GetAllIncludingDeletedAsync"/>
    /// nor <c>GetDeletedOnlyAsync</c> has a paged/limited overload - only these, which always
    /// materialize every matching row - so Skip/Take are applied afterwards, in memory, over the
    /// fully materialized, soft-delete-inclusive result set. Mirrors the same "no paged variant on the
    /// soft-delete repository" situation <c>GetSeekQueryHandler.LoadIncludingDeletedAsync</c> handles
    /// for the seek provider. query.Selector is rejected before reaching here for the same reason
    /// <c>GetAllQueryHandler</c> rejects it: neither soft-delete method has a projected overload
    /// either, so silently returning full entities instead of the caller's projection would be a
    /// silent-wrong-data outcome.
    /// </remarks>
    private static async Task<KyrolusPagedResult<TResponse>> LoadIncludingDeletedAsync(
        IKyrolusMartenSoftDeleteRepositoryAsync<TSession, TResponse, TKey> soft,
        GetPagedQuery<TResponse, TKey> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (query.Selector is not null)
        {
            throw new InvalidOperationException(
                "[Kyrolus CQRS] GetPagedQuery.Selector is not supported when browsing soft-deleted " +
                "records (IncludeDeleted/DeletedOnly) - drop the projection, or query without " +
                "IncludeDeleted/DeletedOnly.");
        }

        var options = BuildOptions(query);
        var all = query.DeletedOnly
            ? await soft.GetDeletedOnlyAsync(options, cancellationToken).ConfigureAwait(false)
            : await soft.GetAllIncludingDeletedAsync(options, cancellationToken).ConfigureAwait(false);

        var list = all.ToList();
        var items = list.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return new KyrolusPagedResult<TResponse>(items, list.Count, pageNumber, pageSize);
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

    private static MartenQueryOptions<TResponse> BuildOptions(GetPagedQuery<TResponse, TKey> query)
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
}

