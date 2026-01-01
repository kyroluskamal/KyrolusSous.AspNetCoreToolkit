
namespace KyrolusSous.CQRS.EF.Command.Patch;


public class PatchCommandHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork, ICacheService cacheService)
: RmoveFromCacheCommon(cacheService), IKyrolusCommandHandler<PatchCommand<TResponse, TKey>, TResponse>
     where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<TResponse> Handle(PatchCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);
        var repo = unitOfWork.GetRepository<IKyrolusCompositeKeyRepositoryAsync<TDbcontext, TResponse, TKey>>();
        var entity = await repo.PatchAsync(command.KeyValues, command.Updates, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity!;
    }
}
