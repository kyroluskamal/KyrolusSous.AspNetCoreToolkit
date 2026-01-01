using KyrolusSous.RedisCaching.Services;

namespace KyrolusSous.CQRS.Marten.Query;

public class GetAllQueryHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork, ICacheService cacheService)
: GetFromCacheCommon<IEnumerable<TResponse>>(cacheService), IKyrolusQueryHandler<GetAllQuery<TResponse>, IEnumerable<TResponse>>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<IEnumerable<TResponse>> Handle(GetAllQuery<TResponse> query, CancellationToken cancellationToken)
    {
        return await GetFromCache(cacheKey: $"{typeof(TResponse).Name}_GetAll", query.Cacheable, async () =>
        {
            var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
            return await repo.GetAllAsync(query.Options, cancellationToken);
        }, cancellationToken);

    }

}
