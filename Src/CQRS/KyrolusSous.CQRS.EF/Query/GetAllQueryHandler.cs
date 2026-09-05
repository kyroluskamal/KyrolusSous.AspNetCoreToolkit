using KyrolusSous.Repositories.EF.Abstractions.Helpers;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.CQRS.EF.Query;

public class GetAllQueryHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
: IKyrolusQueryHandler<GetAllQuery<TResponse>, IEnumerable<TResponse>>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<IEnumerable<TResponse>> Handle(GetAllQuery<TResponse> query, CancellationToken cancellationToken)
    {
        if (query.IncludeDeleted || query.DeletedOnly)
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
                // Neither GetAllIncludingDeletedAsync nor GetDeletedOnlyAsync below has a projected
                // overload, so query.Selector (potentially used to redact PII) would otherwise be
                // silently dropped and full, un-projected entities returned instead - a
                // silent-wrong-data outcome. Failing loudly here instead matches this codebase's
                // "fail closed" precedent for a request this code path cannot honor.
                if (query.Selector is not null)
                {
                    throw new InvalidOperationException(
                        "[Kyrolus CQRS] GetAllQuery.Selector is not supported when browsing soft-deleted " +
                        "records (IncludeDeleted/DeletedOnly) - drop the projection, or query without " +
                        "IncludeDeleted/DeletedOnly.");
                }

                // IncludeProperties is passed separately below, so only IncludeGraph + IncludeExpressions
                // belong in this merge - folding IncludeProperties in here too would duplicate it.
                var graph = KyrolusIncludeMerge.MergeGraph(query.IncludeGraph, query.IncludeExpressions);
                return query.DeletedOnly
                    ? await softRepo.GetDeletedOnlyAsync(
                        query.Filter,
                        query.OrderBy,
                        query.IncludeProperties,
                        includeGraph: graph,
                        asNoTracking: query.AsNoTracking,
                        useSplitQuery: query.UseSplitQuery,
                        cancellationToken)
                    : await softRepo.GetAllIncludingDeletedAsync(
                        query.Filter,
                        query.OrderBy,
                        query.IncludeProperties,
                        includeGraph: graph,
                        asNoTracking: query.AsNoTracking,
                        useSplitQuery: query.UseSplitQuery,
                        cancellationToken);
            }

            if (query.DeletedOnly)
            {
                return Array.Empty<TResponse>();
            }
        }

        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        // Single-pass merge of all three include sources - a two-step merge (IncludeExpressions +
        // IncludeGraph first, IncludeProperties folded in only when that came out empty) silently
        // dropped IncludeProperties whenever it was combined with the other two.
        var includes = KyrolusIncludeMerge.MergeExpressions(query.IncludeProperties, query.IncludeGraph, query.IncludeExpressions) ?? [];
        if (query.Selector is not null)
        {
            var spec = new KyrolusEfQuerySpecification<TResponse, TResponse>(
                new SpecificationInputs<TResponse, TResponse>(
                    Filter: query.Filter,
                    OrderBy: query.OrderBy,
                    IncludeDeleted: false,
                    Selector: query.Selector,
                    Includes: includes,
                    AsNoTracking: query.AsNoTracking ?? false,
                    UseSplitQuery: query.UseSplitQuery ?? false
                ));
            return await repo.QueryAsync(spec, cancellationToken);
        }

        if (includes.Length > 0)
        {
            return await repo.GetAllAsync(
                query.Filter,
                query.OrderBy,
                query.AsNoTracking,
                query.UseSplitQuery,
                cancellationToken,
                includes);
        }

        return await repo.GetAllAsync(
            query.Filter,
            query.OrderBy,
            query.IncludeProperties,
            includeGraph: query.IncludeGraph,
            asNoTracking: query.AsNoTracking,
            useSplitQuery: query.UseSplitQuery,
            cancellationToken);
    }

}
