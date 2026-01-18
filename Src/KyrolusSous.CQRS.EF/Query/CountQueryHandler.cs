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
                var allIncludingDeleted = await softRepo.GetAllIncludingDeletedAsync(
                    query.Filter,
                    orderBy: null,
                    includeProperties: null,
                    includeGraph: null,
                    asNoTracking: true,
                    useSplitQuery: null,
                    cancellationToken).ConfigureAwait(false);
                return allIncludingDeleted.Count;
            }
        }

        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        var spec = new KyrolusEfPagedQuerySpecification<TResponse>(
            query.Filter,
            orderBy: null,
            includes: [],
            pageNumber: 1,
            pageSize: 1,
            asNoTracking: true);
        var (_, total) = await repo.GetPagedAsync(spec, cancellationToken).ConfigureAwait(false);
        return total;
    }
}
