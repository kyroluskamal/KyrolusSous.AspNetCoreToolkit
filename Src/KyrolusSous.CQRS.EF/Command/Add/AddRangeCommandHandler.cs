namespace KyrolusSous.CQRS.EF.Command.Add;

public class AddRangeCommandHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
: IKyrolusCommandHandler<AddRangeCommand<TResponse>, IEnumerable<TResponse>>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<IEnumerable<TResponse>> Handle(AddRangeCommand<TResponse> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        var entities = await repo.AddRangeAsync(command.Entities, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entities;
    }
}
