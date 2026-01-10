using KyrolusSous.CQRS.Abstractions.Models;
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
        var includes = query.IncludeExpressions ?? [];
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
