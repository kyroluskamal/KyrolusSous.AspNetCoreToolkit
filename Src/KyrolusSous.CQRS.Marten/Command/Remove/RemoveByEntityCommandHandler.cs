using KyrolusSous.RedisCaching.Services;
using KyrolusSous.Mediator.Abstractions.Interfaces;

namespace KyrolusSous.CQRS.Marten.Command.Remove;

public class RemoveByEntityCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork, ICacheService cacheService)
: RmoveFromCacheCommon(cacheService), IKyrolusCommandHandler<RemoveByEntityCommand<TResponse>>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public virtual async Task Handle(RemoveByEntityCommand<TResponse> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        await repo.RemoveAsync(command.Entity, command.ExpectedVersion, command.TenantId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
