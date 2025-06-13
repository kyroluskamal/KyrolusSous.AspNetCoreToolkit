using KyrolusSous.IRespositoryInterfaces.IUnitOfWork;
using KyrolusSous.RedisCaching.Services;

namespace KyrolusSous.CQRS.Base.Query;

public class GetAllQueryHandler<TDbcontext, TResponse, TKey>(IUnitOfWork<TDbcontext> unitOfWork, ICacheService cacheService)
: GetFromCacheCommon<IEnumerable<TResponse>>(cacheService), IQueryHandler<GetAllQuery<TResponse>, IEnumerable<TResponse>>
    where TDbcontext : class
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<IEnumerable<TResponse>> Handle(GetAllQuery<TResponse> query, CancellationToken cancellationToken)
    {
        return await GetFromCache(cacheKey: $"{typeof(TResponse).Name}_GetAll", query.Cacheable, async () =>
        {
            return await unitOfWork.Repository<TResponse, TKey>().GetAllAsync(
                            query.Filter,
                            query.OrderBy,
                            query.IncludeProperties,
                            cancellationToken);
        }, cancellationToken);

    }

}
