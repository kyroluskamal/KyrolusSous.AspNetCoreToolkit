

using KyrolusSous.IRespositoryInterfaces.IUnitOfWork;
using KyrolusSous.RedisCaching.Services;
using KyrolusSous.SourceMediator.Interfaces;

namespace KyrolusSous.CQRS.Base.Command.Update;

public class UpdateRangeCommandHandler<TDbcontext, TResponse, TKey>(IUnitOfWork<TDbcontext> unitOfWork, ICacheService cacheService)
 : RmoveFromCacheCommon(cacheService), ICommandHandler<UpdateRangeCommand<TResponse>, IEnumerable<TResponse>>
    where TDbcontext : class
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<IEnumerable<TResponse>> Handle(UpdateRangeCommand<TResponse> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);
        var entities = await unitOfWork.Repository<TResponse, TKey>().UpdateRangeAsync(command.Entities);
        await unitOfWork.SaveAsync<TResponse>();
        return entities;
    }
}
