using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.CQRS.EF.Query;

public class CountQueryHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
    : IKyrolusQueryHandler<CountQuery<TResponse>, long>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<long> Handle(CountQuery<TResponse> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        var spec = new KyrolusEfPagedQuerySpecification<TResponse>(
            new SpecificationInputs<TResponse, TResponse>(
                Filter: query.Filter,
                OrderBy: null,
                AsNoTracking: true,
                UseSplitQuery: false,
                Includes: null,
                IncludeDeleted: query.IncludeDeleted,
                Selector: null
                ),
            pageNumber: 1,
            pageSize: 1);
        var (_, total) = await repo.GetPagedAsync(spec, cancellationToken).ConfigureAwait(false);
        return total;
    }
}
