
namespace KyrolusSous.CQRS.Marten.Query;

public class GetByIdQueryHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
: IKyrolusQueryHandler<GetByIdQuery<TResponse, TKey>, MartenEntityResult<TResponse>?>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<MartenEntityResult<TResponse>?> Handle(GetByIdQuery<TResponse, TKey> query, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        return await repo.GetByIdAsync(query.Id, query.Options, cancellationToken);
    }
}

