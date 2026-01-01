
namespace KyrolusSous.CQRS.EF.Command.Add;

public class AddCommandHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork, ICacheService cacheService)
    : RmoveFromCacheCommon(cacheService), IKyrolusCommandHandler<AddCommand<TResponse>, TResponse>
        where TDbcontext : DbContext
        where TResponse : class
        where TKey : IEquatable<TKey>
{
    public async Task<TResponse> Handle(AddCommand<TResponse> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);

        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        var entity = await repo.AddAsync(command.Entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity;
    }
}

