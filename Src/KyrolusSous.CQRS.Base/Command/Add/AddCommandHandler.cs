using KyrolusSous.IRespositoryInterfaces.IUnitOfWork;
using KyrolusSous.RedisCaching.Services;
using KyrolusSous.SourceMediator.Interfaces;

namespace KyrolusSous.CQRS.Base.Command.Add;

public class AddCommandHandler<TDbcontext, TResponse, TKey>(IUnitOfWork<TDbcontext> unitOfWork, ICacheService cacheService)
    : RmoveFromCacheCommon(cacheService), ICommandHandler<AddCommand<TResponse>, TResponse>
        where TDbcontext : class
        where TResponse : class
        where TKey : IEquatable<TKey>
{
    public async Task<TResponse> Handle(AddCommand<TResponse> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);

        var entity = await unitOfWork.Repository<TResponse, TKey>().AddAsync(command.Entity, cancellationToken);
        await unitOfWork.SaveAsync<TResponse>();
        return entity;
    }
}

