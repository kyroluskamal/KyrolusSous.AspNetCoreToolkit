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
        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        return await repo.GetAllAsync(
                        query.Filter,
                        query.OrderBy,
                        query.IncludeProperties,
                        includeGraph: null,
                        asNoTracking: null,
                        useSplitQuery: null,
                        cancellationToken);
    }

}
