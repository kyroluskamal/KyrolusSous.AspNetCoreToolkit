
namespace KyrolusSous.CQRS.Marten.Command.Add;

public class AddCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusCommandHandler<AddCommand<TResponse>, TResponse>
        where TSession : class, IDocumentSession
        where TResponse : class
        where TKey : IEquatable<TKey>
{
    public async Task<TResponse> Handle(AddCommand<TResponse> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var entity = await repo.AddAsync(command.Entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity;
    }
}
