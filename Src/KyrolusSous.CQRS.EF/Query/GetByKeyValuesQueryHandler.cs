using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.CQRS.EF.Query;

public class GetByKeyValuesQueryHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
    : IKyrolusQueryHandler<GetByKeyValuesQuery<TResponse, TKey>, TResponse?>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<TResponse?> Handle(GetByKeyValuesQuery<TResponse, TKey> query, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusCompositeKeyRepositoryAsync<TDbcontext, TResponse, TKey>>();
        var includeExpressions = query.IncludeExpressions;
        if (includeExpressions is not null && includeExpressions.Length > 0)
        {
            return await repo.GetByIdAsync(
                query.KeyValues,
                query.AsNoTracking,
                query.UseSplitQuery,
                cancellationToken,
                includeExpressions);
        }

        return await repo.GetByIdAsync(
            query.KeyValues,
            query.IncludeProperties,
            includeGraph: null,
            asNoTracking: query.AsNoTracking,
            useSplitQuery: query.UseSplitQuery,
            cancellationToken);
    }
}
