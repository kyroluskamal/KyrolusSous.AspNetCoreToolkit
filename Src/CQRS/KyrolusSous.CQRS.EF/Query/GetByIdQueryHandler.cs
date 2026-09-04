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
        // Single-pass merge of all three include sources - a two-step merge (IncludeExpressions +
        // IncludeGraph first, IncludeProperties folded in only when that came out empty) silently
        // dropped IncludeProperties whenever it was combined with the other two, and re-merging
        // IncludeGraph into an already-merged array duplicated its entries.
        var includes = KyrolusIncludeMerge.MergeExpressions(query.IncludeProperties, query.IncludeGraph, query.IncludeExpressions) ?? [];
        if (query.IncludeDeleted)
        {
            IKyrolusSingleKeySoftDeleteRepository<TResponse, TKey>? softRepo = null;
            try
            {
                softRepo = unitOfWork.GetRepository<IKyrolusSingleKeySoftDeleteRepository<TResponse, TKey>>();
            }
            catch (InvalidOperationException ex) when (ex.IsRepositoryNotRegistered())
            {
                softRepo = null;
            }

            if (softRepo is not null)
            {
                return await softRepo.GetByIdIncludingDeletedAsync(
                    query.Id,
                    query.AsNoTracking,
                    query.UseSplitQuery,
                    cancellationToken,
                    includes);
            }
        }

        var repo = unitOfWork.GetRepository<IKyrolusSingleKeyRepositoryAsync<TDbcontext, TResponse, TKey>>();
        if (includes.Length > 0)
        {
            return await repo.GetByIdAsync(
                query.Id,
                query.AsNoTracking,
                query.UseSplitQuery,
                cancellationToken,
                includes);
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

