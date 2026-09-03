using KyrolusSous.CQRS.Abstractions.Models;
using KyrolusSous.Repositories.EF.Abstractions.Helpers;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.CQRS.EF.Query;

public sealed class GetPagedQueryHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
    : IKyrolusQueryHandler<GetPagedQuery<TResponse, TKey>, KyrolusPagedResult<TResponse>>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<KyrolusPagedResult<TResponse>> Handle(GetPagedQuery<TResponse, TKey> query, CancellationToken cancellationToken)
    {
        // Clamp caller-supplied paging so PageSize = int.MaxValue (or negative) can't force the
        // database to attempt to materialize an enormous or malformed result set.
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, KyrolusPagingLimits.MaxPageSize);

        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        var includes = KyrolusIncludeMerge.MergeExpressions(query.IncludeProperties, query.IncludeGraph, query.IncludeExpressions) ?? [];
        if (query.Selector is not null)
        {
            var spec = new KyrolusEfPagedQuerySpecification<TResponse>(
                new SpecificationInputs<TResponse, TResponse>(
                    Filter: query.Filter,
                    OrderBy: query.OrderBy,
                    AsNoTracking: query.AsNoTracking ?? false,
                    UseSplitQuery: query.UseSplitQuery ?? false,
                    IncludeDeleted: false,
                    Selector: query.Selector,
                    Includes: includes
                ),
                pageNumber,
                pageSize);
            var (projectedItems, projectedTotal) = await repo.GetPagedAsync(spec, cancellationToken);
            return new KyrolusPagedResult<TResponse>(projectedItems, projectedTotal, pageNumber, pageSize);
        }

        var specification = new KyrolusEfPagedQuerySpecification<TResponse>(
           new SpecificationInputs<TResponse, TResponse>(
                    Filter: query.Filter,
                    OrderBy: query.OrderBy,
                    AsNoTracking: query.AsNoTracking ?? false,
                    UseSplitQuery: query.UseSplitQuery ?? false,
                    IncludeDeleted: false,
                    Selector: query.Selector,
                    Includes: includes
                ),
            pageNumber,
            pageSize);

        var (items, total) = await repo.GetPagedWithDefaultsAsync(
            specification,
            filter: query.Filter,
            orderBy: query.OrderBy,
            asNoTracking: query.AsNoTracking,
            useSplitQuery: query.UseSplitQuery,
            cancellationToken: cancellationToken,
            includeExpressions: includes);

        return new KyrolusPagedResult<TResponse>(items, total, pageNumber, pageSize);
    }
}
