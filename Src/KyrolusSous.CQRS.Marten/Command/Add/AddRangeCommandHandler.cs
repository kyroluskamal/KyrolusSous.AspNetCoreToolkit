namespace KyrolusSous.CQRS.Marten.Command.Add;

public class AddRangeCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork, ICacheService cacheService)
    : RmoveFromCacheCommon(cacheService), IKyrolusCommandHandler<AddRangeCommand<TResponse>, IEnumerable<TResponse>>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<IEnumerable<TResponse>> Handle(AddRangeCommand<TResponse> command, CancellationToken cancellationToken)
    {
        await RemoveKeysByPatternAsync(command.Cacheable, typeof(TResponse).Name, cancellationToken);
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var entities = await repo.AddRangeAsync(command.Entities, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entities;
    }
}
