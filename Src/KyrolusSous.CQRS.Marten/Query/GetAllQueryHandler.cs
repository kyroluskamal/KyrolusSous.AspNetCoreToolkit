
namespace KyrolusSous.CQRS.Marten.Query;

public class GetAllQueryHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
: IKyrolusQueryHandler<GetAllQuery<TResponse>, IEnumerable<TResponse>>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<IEnumerable<TResponse>> Handle(GetAllQuery<TResponse> query, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        return await repo.GetAllAsync(query.Options, cancellationToken);
    }

}
