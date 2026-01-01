using KyrolusSous.RedisCaching.Services;
using KyrolusSous.Mediator.Abstractions.Interfaces;

namespace KyrolusSous.CQRS.Marten.Command.Remove;

public class RemoveByIdCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork, ICacheService cacheService) : RmoveFromCacheCommon(cacheService), IKyrolusCommandHandler<RemoveByIdCommand<TResponse, TKey>>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public virtual async Task Handle(RemoveByIdCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        await repo.RemoveAsync(command.Id, command.ExpectedVersion, command.TenantId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
