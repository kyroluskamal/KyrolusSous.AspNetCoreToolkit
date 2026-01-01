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
        var repo = unitOfWork.GetRepository<IKyrolusSingleKeyRepositoryAsync<TDbcontext, TResponse, TKey>>();
        return await repo.GetByIdAsync(
               query.Id,
               query.IncludeProperties,
               includeGraph: null,
               asNoTracking: null,
               useSplitQuery: null,
               cancellationToken);
    }
}

