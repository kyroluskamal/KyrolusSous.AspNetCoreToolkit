
namespace KyrolusSous.CQRS.Marten.Command.Patch;


public class PatchCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
: IKyrolusCommandHandler<PatchCommand<TResponse, TKey>, MartenEntityResult<TResponse>?>
     where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<MartenEntityResult<TResponse>?> Handle(PatchCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var entity = await repo.PatchAsync(command.Id, command.Updates, command.TenantId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity;
    }
}
