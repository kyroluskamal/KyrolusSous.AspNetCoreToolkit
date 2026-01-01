namespace KyrolusSous.CQRS.EF.Command.Update;

public class UpdateRangeCommandHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
 : IKyrolusCommandHandler<UpdateRangeCommand<TResponse>, IEnumerable<TResponse>>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<IEnumerable<TResponse>> Handle(UpdateRangeCommand<TResponse> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        var entities = await repo.UpdateRangeAsync(command.Entities, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entities;
    }
}
