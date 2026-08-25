namespace KyrolusSous.CQRS.Marten.Query;

public class CountQueryHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusQueryHandler<CountQuery<TResponse>, long>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<long> Handle(CountQuery<TResponse> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var options = new MartenQueryOptions<TResponse>(
            Filter: query.Filter,
            OrderBy: null,
            IncludeProperties: null,
            IncludeExpressions: null,
            TenantId: query.TenantId,
            IncludeSoftDeleted: query.IncludeDeleted);

        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var page = new MartenPageRequest(1, 1);
        var pageResult = await repo.GetPageAsync(options, page, cancellationToken).ConfigureAwait(false);
        return pageResult.TotalCount;
    }
}

