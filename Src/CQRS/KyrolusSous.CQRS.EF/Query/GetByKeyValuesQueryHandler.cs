using KyrolusSous.Repositories.EF.Abstractions.Helpers;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
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
        var mergedExpressions = KyrolusIncludeMerge.MergeExpressions(query.IncludeExpressions, query.IncludeGraph);
        if (query.IncludeDeleted)
        {
            IKyrolusCompositeKeySoftDeleteRepository<TResponse>? softRepo = null;
            try
            {
                softRepo = unitOfWork.GetRepository<IKyrolusCompositeKeySoftDeleteRepository<TResponse>>();
            }
            catch (InvalidOperationException ex) when (ex.IsRepositoryNotRegistered())
            {
                softRepo = null;
            }

            if (softRepo is not null)
            {
                var includes = KyrolusIncludeMerge.MergeExpressions(query.IncludeProperties, query.IncludeGraph, mergedExpressions) ?? [];
                return await softRepo.GetByIdIncludingDeletedAsync(
                    query.KeyValues,
                    query.AsNoTracking,
                    query.UseSplitQuery,
                    cancellationToken,
                    includes);
            }
        }

        var repo = unitOfWork.GetRepository<IKyrolusCompositeKeyRepositoryAsync<TDbcontext, TResponse, TKey>>();
        if (mergedExpressions is not null && mergedExpressions.Length > 0)
        {
            return await repo.GetByIdAsync(
                query.KeyValues,
                query.AsNoTracking,
                query.UseSplitQuery,
                cancellationToken,
                mergedExpressions);
        }

        return await repo.GetByIdAsync(
            query.KeyValues,
            query.IncludeProperties,
            includeGraph: query.IncludeGraph,
            asNoTracking: query.AsNoTracking,
            useSplitQuery: query.UseSplitQuery,
            cancellationToken);
    }
}
