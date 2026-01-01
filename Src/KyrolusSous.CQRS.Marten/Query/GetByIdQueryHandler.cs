using KyrolusSous.RedisCaching.Services;

namespace KyrolusSous.CQRS.Marten.Query;

public class GetByIdQueryHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork, ICacheService cacheService)
: GetFromCacheCommon<MartenEntityResult<TResponse>>(cacheService), IKyrolusQueryHandler<GetByIdQuery<TResponse, TKey>, MartenEntityResult<TResponse>>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<MartenEntityResult<TResponse>> Handle(GetByIdQuery<TResponse, TKey> query, CancellationToken cancellationToken)
    {
        return await GetFromCache(cacheKey: $"{typeof(TResponse).Name}_GetById_{query.Id}", query.Cacheable, async () =>
        {
            var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
            return await repo.GetByIdAsync(query.Id, query.Options, cancellationToken)
                ?? throw new NotFoundException(typeof(TResponse).Name, query.Id?.ToString() ?? string.Empty);
        }, cancellationToken);

    }
}

