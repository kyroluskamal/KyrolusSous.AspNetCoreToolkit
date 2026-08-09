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
        var mergedExpressions = KyrolusIncludeMerge.MergeExpressions(query.IncludeExpressions, query.IncludeGraph);
        if (query.IncludeDeleted || query.DeletedOnly)
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
                var graph = KyrolusIncludeMerge.MergeGraph(query.IncludeGraph, mergedExpressions);
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
        if (query.Selector is not null)
        {
            var includes = KyrolusIncludeMerge.MergeExpressions(query.IncludeProperties, query.IncludeGraph, mergedExpressions) ?? [];
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

        if (mergedExpressions is not null && mergedExpressions.Length > 0)
        {
            return await repo.GetAllAsync(
                query.Filter,
                query.OrderBy,
                query.AsNoTracking,
                query.UseSplitQuery,
                cancellationToken,
                mergedExpressions);
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
