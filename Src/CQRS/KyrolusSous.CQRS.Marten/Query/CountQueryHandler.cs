namespace KyrolusSous.CQRS.Marten.Query;

public class CountQueryHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusQueryHandler<CountQuery<TResponse>, long>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<long> Handle(CountQuery<TResponse> query, CancellationToken cancellationToken)
    {
        var options = new MartenQueryOptions<TResponse>(
            Filter: query.Filter,
            OrderBy: null,
            IncludeProperties: null,
            IncludeExpressions: null,
            TenantId: query.TenantId,
            IncludeSoftDeleted: query.IncludeDeleted);

        if (query.IncludeDeleted)
        {
            var softRepo = TryResolveSoftRepository();
            if (softRepo is not null)
            {
                // Get all including deleted and count them
                var items = await softRepo.GetAllIncludingDeletedAsync(options, cancellationToken).ConfigureAwait(false);
                return items.Count();
            }
        }

        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var page = new MartenPageRequest(1, 1);
        var pageResult = await repo.GetPageAsync(options, page, cancellationToken).ConfigureAwait(false);
        return pageResult.TotalCount;
    }

    private IKyrolusMartenSoftDeleteRepositoryAsync<TSession, TResponse, TKey>? TryResolveSoftRepository()
    {
        try
        {
            return unitOfWork.GetRepository<IKyrolusMartenSoftDeleteRepositoryAsync<TSession, TResponse, TKey>>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}

