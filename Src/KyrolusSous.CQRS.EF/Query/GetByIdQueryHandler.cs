using KyrolusSous.RedisCaching.Services;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.CQRS.EF.Query;

public class GetByIdQueryHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork, ICacheService cacheService)
: GetFromCacheCommon<TResponse>(cacheService), IKyrolusQueryHandler<GetByIdQuery<TResponse, TKey>, TResponse>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<TResponse> Handle(GetByIdQuery<TResponse, TKey> query, CancellationToken cancellationToken)
    {
        return await GetFromCache(cacheKey: $"{typeof(TResponse).Name}_GetById_{query.Id}", query.Cacheable, async () =>
                {
                    var repo = unitOfWork.GetRepository<IKyrolusSingleKeyRepositoryAsync<TDbcontext, TResponse, TKey>>();
                    return await repo.GetByIdAsync(
                           query.Id,
                           query.IncludeProperties,
                           includeGraph: null,
                           asNoTracking: null,
                           useSplitQuery: null,
                           cancellationToken) ?? throw new NotFoundException(typeof(TResponse).Name, query.Id!.ToString() ?? string.Empty);
                }, cancellationToken);

    }
}

