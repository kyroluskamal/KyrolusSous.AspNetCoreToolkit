using KyrolusSous.IRespositoryInterfaces.IUnitOfWork;
using KyrolusSous.RedisCaching.Services;
using KyrolusSous.SourceMediator.Interfaces;

namespace KyrolusSous.CQRS.Base.Command.Add;

public class AddRangeCommandHandler<TDbcontext, TResponse, TKey>(IUnitOfWork<TDbcontext> unitOfWork, ICacheService cacheService)
: RmoveFromCacheCommon(cacheService), ICommandHandler<AddRangeCommand<TResponse>, IEnumerable<TResponse>>
    where TDbcontext : class
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<IEnumerable<TResponse>> Handle(AddRangeCommand<TResponse> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);
        var entities = await unitOfWork.Repository<TResponse, TKey>().AddRangeAsync(command.Entities, cancellationToken);
        await unitOfWork.SaveAsync<TResponse>();
        return entities;
    }
}
