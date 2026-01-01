using KyrolusSous.RedisCaching.Services;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.CQRS.EF.Command.Remove;

public class RemoveByIdCommandHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork, ICacheService cacheService) : RmoveFromCacheCommon(cacheService), IKyrolusCommandHandler<RemoveByIdCommand<TResponse, TKey>>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public virtual async Task Handle(RemoveByIdCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);
        var repo = unitOfWork.GetRepository<IKyrolusCompositeKeyRepositoryAsync<TDbcontext, TResponse, TKey>>();
        await repo.RemoveAsync(command.KeyValues, cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
