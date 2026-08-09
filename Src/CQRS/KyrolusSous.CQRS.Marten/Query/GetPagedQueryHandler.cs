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
        var options = BuildOptions(query);
        var page = new MartenPageRequest(query.PageNumber, query.PageSize);
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        if (query.Selector is not null)
        {
            var projected = await repo.QueryPageAsync<TResponse>(options, q => (global::Marten.Linq.IMartenQueryable<TResponse>)q.Select(query.Selector), page, cancellationToken).ConfigureAwait(false);
            return new KyrolusPagedResult<TResponse>(projected.Items.ToList(), (int)projected.TotalCount, projected.PageNumber, projected.PageSize);
        }

        var result = await repo.GetPageAsync(options, page, cancellationToken).ConfigureAwait(false);
        return new KyrolusPagedResult<TResponse>(result.Items.ToList(), (int)result.TotalCount, result.PageNumber, result.PageSize);
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
            IncludeSoftDeleted: false);
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

