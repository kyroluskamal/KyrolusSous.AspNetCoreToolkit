namespace KyrolusSous.CQRS.Marten.Command.Update;

public class UpdateCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork, ICacheService cacheService)
: RmoveFromCacheCommon(cacheService), IKyrolusCommandHandler<UpdateCommand<TResponse>, TResponse>
     where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<TResponse> Handle(UpdateCommand<TResponse> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var entity = await repo.UpdateAsync(command.Entity, command.ExpectedVersion, command.TenantId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity!;
    }
}
