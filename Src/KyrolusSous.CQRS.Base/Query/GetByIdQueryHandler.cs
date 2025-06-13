using KyrolusSous.IRespositoryInterfaces.IUnitOfWork;
using KyrolusSous.RedisCaching.Services;

namespace KyrolusSous.CQRS.Base.Query;

public class GetByIdQueryHandler<TDbcontext, TResponse, TKey>(IUnitOfWork<TDbcontext> unitOfWork, ICacheService cacheService)
: GetFromCacheCommon<TResponse>(cacheService), IQueryHandler<GetByIdQuery<TResponse, TKey>, TResponse>
    where TDbcontext : class
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<TResponse> Handle(GetByIdQuery<TResponse, TKey> query, CancellationToken cancellationToken)
    {
        return await GetFromCache(cacheKey: $"{typeof(TResponse).Name}_GetById_{query.Id}", query.Cacheable, async () =>
                {
                    return await unitOfWork.Repository<TResponse, TKey>().GetByIdAsync(
                           query.Id, query.IncludeProperties,
                            cancellationToken) ?? throw new NotFoundException(typeof(TResponse).Name, query.Id!.ToString() ?? string.Empty);
                }, cancellationToken);

    }
}

