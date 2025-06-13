using KyrolusSous.IRespositoryInterfaces.IUnitOfWork;
using KyrolusSous.RedisCaching.Services;
using KyrolusSous.SourceMediator.Interfaces;

namespace KyrolusSous.CQRS.Base.Command.Remove;

public class RemoveRangeHandler<TDbcontext, TResponse, TKey>(IUnitOfWork<TDbcontext> unitOfWork, ICacheService cacheService) :
    RmoveFromCacheCommon(cacheService),
 ICommandHandler<RemoveRangeCommand<TResponse>>
 where TDbcontext : class
 where TResponse : class
 where TKey : IEquatable<TKey>
{
    public async Task Handle(RemoveRangeCommand<TResponse> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);
        var respo = unitOfWork.Repository<TResponse, TKey>();
        await respo.RemoveRangeAsync(command.Entities);
        await unitOfWork.SaveAsync<TResponse>();
    }
}
