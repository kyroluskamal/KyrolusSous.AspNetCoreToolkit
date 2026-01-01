
namespace KyrolusSous.CQRS.Marten.Command.Patch;


public class PatchCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork, ICacheService cacheService)
: RmoveFromCacheCommon(cacheService), IKyrolusCommandHandler<PatchCommand<TResponse, TKey>, MartenEntityResult<TResponse>>
     where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<MartenEntityResult<TResponse>> Handle(PatchCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var entity = await repo.PatchAsync(command.Id, command.Updates, command.TenantId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity ?? throw new NotFoundException(typeof(TResponse).Name, command.Id?.ToString() ?? string.Empty);
    }
}
