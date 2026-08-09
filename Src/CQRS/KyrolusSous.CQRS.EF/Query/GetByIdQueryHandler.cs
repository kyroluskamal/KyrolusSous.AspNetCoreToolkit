using KyrolusSous.Repositories.EF.Abstractions.Helpers;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.CQRS.EF.Query;

public class GetByIdQueryHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
: IKyrolusQueryHandler<GetByIdQuery<TResponse, TKey>, TResponse?>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<TResponse?> Handle(GetByIdQuery<TResponse, TKey> query, CancellationToken cancellationToken)
    {
        var mergedExpressions = KyrolusIncludeMerge.MergeExpressions(query.IncludeExpressions, query.IncludeGraph);
        if (query.IncludeDeleted)
        {
            IKyrolusSingleKeySoftDeleteRepository<TResponse, TKey>? softRepo = null;
            try
            {
                softRepo = unitOfWork.GetRepository<IKyrolusSingleKeySoftDeleteRepository<TResponse, TKey>>();
            }
            catch (InvalidOperationException)
            {
                softRepo = null;
            }

            if (softRepo is not null)
            {
                var includes = KyrolusIncludeMerge.MergeExpressions(query.IncludeProperties, query.IncludeGraph, mergedExpressions) ?? [];
                return await softRepo.GetByIdIncludingDeletedAsync(
                    query.Id,
                    query.AsNoTracking,
                    query.UseSplitQuery,
                    cancellationToken,
                    includes);
            }
        }

        var repo = unitOfWork.GetRepository<IKyrolusSingleKeyRepositoryAsync<TDbcontext, TResponse, TKey>>();
        if (mergedExpressions is not null && mergedExpressions.Length > 0)
        {
            return await repo.GetByIdAsync(
                query.Id,
                query.AsNoTracking,
                query.UseSplitQuery,
                cancellationToken,
                mergedExpressions);
        }

        return await repo.GetByIdAsync(
            query.Id,
            query.IncludeProperties,
            includeGraph: query.IncludeGraph,
            asNoTracking: query.AsNoTracking,
            useSplitQuery: query.UseSplitQuery,
            cancellationToken);
    }
}

