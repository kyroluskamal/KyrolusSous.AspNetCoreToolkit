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
        // Single-pass merge of all three include sources - a two-step merge (IncludeExpressions +
        // IncludeGraph first, IncludeProperties folded in only when that came out empty) silently
        // dropped IncludeProperties whenever it was combined with the other two, and re-merging
        // IncludeGraph into an already-merged array duplicated its entries.
        var includes = KyrolusIncludeMerge.MergeExpressions(query.IncludeProperties, query.IncludeGraph, query.IncludeExpressions) ?? [];
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
                return await softRepo.GetByIdIncludingDeletedAsync(
                    query.KeyValues,
                    query.AsNoTracking,
                    query.UseSplitQuery,
                    cancellationToken,
                    includes);
            }
        }

        var repo = unitOfWork.GetRepository<IKyrolusCompositeKeyRepositoryAsync<TDbcontext, TResponse, TKey>>();
        if (includes.Length > 0)
        {
            return await repo.GetByIdAsync(
                query.KeyValues,
                query.AsNoTracking,
                query.UseSplitQuery,
                cancellationToken,
                includes);
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
