using KyrolusSous.RedisCaching.Services;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.CQRS.EF.Query;

public class GetAllQueryHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork, ICacheService cacheService)
: GetFromCacheCommon<IEnumerable<TResponse>>(cacheService), IKyrolusQueryHandler<GetAllQuery<TResponse>, IEnumerable<TResponse>>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<IEnumerable<TResponse>> Handle(GetAllQuery<TResponse> query, CancellationToken cancellationToken)
    {
        return await GetFromCache(cacheKey: $"{typeof(TResponse).Name}_GetAll", query.Cacheable, async () =>
        {
            var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
            return await repo.GetAllAsync(
                            query.Filter,
                            query.OrderBy,
                            query.IncludeProperties,
                            includeGraph: null,
                            asNoTracking: null,
                            useSplitQuery: null,
                            cancellationToken);
        }, cancellationToken);

    }

}
