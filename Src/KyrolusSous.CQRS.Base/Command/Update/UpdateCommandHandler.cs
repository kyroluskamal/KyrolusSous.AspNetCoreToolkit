using KyrolusSous.IRespositoryInterfaces.IUnitOfWork;
using KyrolusSous.RedisCaching.Services;
using KyrolusSous.SourceMediator.Interfaces;

namespace KyrolusSous.CQRS.Base.Command.Update;

public class UpdateCommandHandler<TDbcontext, TResponse, TKey>(IUnitOfWork<TDbcontext> unitOfWork, ICacheService cacheService)
: RmoveFromCacheCommon(cacheService), ICommandHandler<UpdateCommand<TResponse>, TResponse>
     where TDbcontext : class
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<TResponse> Handle(UpdateCommand<TResponse> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);
        var entity = await unitOfWork.Repository<TResponse, TKey>().UpdateAsync(command.Entity);
        await unitOfWork.SaveAsync<TResponse>();
        return entity!;
    }
}
