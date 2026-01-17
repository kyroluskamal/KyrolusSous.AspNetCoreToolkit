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
        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        var includes = KyrolusIncludeMerge.MergeExpressions(query.IncludeProperties, query.IncludeGraph, query.IncludeExpressions) ?? [];
        if (query.Selector is not null)
        {
            var spec = new KyrolusEfPagedQuerySpecification<TResponse>(
                query.Filter,
                query.OrderBy,
                includes,
                query.PageNumber,
                query.PageSize,
                query.AsNoTracking ?? false,
                query.Selector);
            var (projectedItems, projectedTotal) = await repo.GetPagedAsync(spec, cancellationToken);
            return new KyrolusPagedResult<TResponse>(projectedItems, projectedTotal, query.PageNumber, query.PageSize);
        }

        var specification = new KyrolusEfPagedQuerySpecification<TResponse>(
            query.Filter,
            query.OrderBy,
            includes,
            query.PageNumber,
            query.PageSize,
            query.AsNoTracking ?? false);

        var (items, total) = await repo.GetPagedWithDefaultsAsync(
            specification,
            filter: query.Filter,
            orderBy: query.OrderBy,
            asNoTracking: query.AsNoTracking,
            useSplitQuery: query.UseSplitQuery,
            cancellationToken: cancellationToken,
            includeExpressions: includes);

        return new KyrolusPagedResult<TResponse>(items, total, query.PageNumber, query.PageSize);
    }
}
