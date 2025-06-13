using KyrolusSous.IRespositoryInterfaces.IUnitOfWork;
using KyrolusSous.RedisCaching.Services;
using KyrolusSous.SourceMediator.Interfaces;

namespace KyrolusSous.CQRS.Base.Command.Remove;

public class RemoveByIdCommandHandler<TDbcontext, TResponse, TKey>(IUnitOfWork<TDbcontext> unitOfWork, ICacheService cacheService) : RmoveFromCacheCommon(cacheService), ICommandHandler<RemoveByIdCommand<TResponse, TKey>>
    where TDbcontext : class
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public virtual async Task Handle(RemoveByIdCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);
        await unitOfWork.Repository<TResponse, TKey>().Remove(command.KeyValues);
        await unitOfWork.SaveAsync<TResponse>();
    }
}
