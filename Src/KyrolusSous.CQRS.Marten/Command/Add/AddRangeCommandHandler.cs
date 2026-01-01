namespace KyrolusSous.CQRS.Marten.Command.Add;

public class AddRangeCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusCommandHandler<AddRangeCommand<TResponse>, IEnumerable<TResponse>>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<IEnumerable<TResponse>> Handle(AddRangeCommand<TResponse> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var entities = await repo.AddRangeAsync(command.Entities, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entities;
    }
}
