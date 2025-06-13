
using KyrolusSous.IRespositoryInterfaces.IUnitOfWork;
using KyrolusSous.RedisCaching.Services;
using KyrolusSous.SourceMediator.Interfaces;

namespace KyrolusSous.CQRS.Base.Command.Patch;


public class PatchCommandHandler<TDbcontext, TResponse, TKey>(IUnitOfWork<TDbcontext> unitOfWork, ICacheService cacheService)
: RmoveFromCacheCommon(cacheService), ICommandHandler<PatchCommand<TResponse, TKey>, TResponse>
     where TDbcontext : class
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<TResponse> Handle(PatchCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);
        var entity = await unitOfWork.Repository<TResponse, TKey>().PatchAsync(command.KeyValues, command.Updates);
        await unitOfWork.SaveAsync<TResponse>();
        return entity!;
    }
}
