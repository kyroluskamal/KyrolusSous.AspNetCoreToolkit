using KyrolusSous.IRespositoryInterfaces.IUnitOfWork;
using KyrolusSous.RedisCaching.Services;
using KyrolusSous.SourceMediator.Interfaces;

namespace KyrolusSous.CQRS.Base.Command.Remove;

public class RemoveByEntityCommandHandler<TDbcontext, TResponse, TKey>(IUnitOfWork<TDbcontext> unitOfWork, ICacheService cacheService)
: RmoveFromCacheCommon(cacheService), ICommandHandler<RemoveByEntityCommand<TResponse>>
    where TDbcontext : class
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public virtual async Task Handle(RemoveByEntityCommand<TResponse> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);
        await unitOfWork.Repository<TResponse, TKey>().Remove(command.Entity);
        await unitOfWork.SaveAsync<TResponse>();
    }
}
