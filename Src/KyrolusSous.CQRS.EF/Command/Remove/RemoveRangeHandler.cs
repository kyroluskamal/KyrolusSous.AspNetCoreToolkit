using KyrolusSous.RedisCaching.Services;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.CQRS.EF.Command.Remove;

public class RemoveRangeHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork, ICacheService cacheService) :
    RmoveFromCacheCommon(cacheService),
 IKyrolusCommandHandler<RemoveRangeCommand<TResponse>>
 where TDbcontext : DbContext
 where TResponse : class
 where TKey : IEquatable<TKey>
{
    public async Task Handle(RemoveRangeCommand<TResponse> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);
        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        await repo.RemoveRangeAsync(command.Entities, cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
