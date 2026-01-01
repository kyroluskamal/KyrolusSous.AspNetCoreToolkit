using KyrolusSous.RedisCaching.Services;
using KyrolusSous.Mediator.Abstractions.Interfaces;

namespace KyrolusSous.CQRS.Marten.Command.Remove;

public class RemoveRangeHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork, ICacheService cacheService) :
    RmoveFromCacheCommon(cacheService),
 IKyrolusCommandHandler<RemoveRangeCommand<TResponse>>
 where TSession : class, IDocumentSession
 where TResponse : class
 where TKey : IEquatable<TKey>
{
    public async Task Handle(RemoveRangeCommand<TResponse> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        await repo.RemoveRangeAsync(command.Entities, command.TenantId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
