namespace KyrolusSous.CQRS.EF.Command.Update;

public class UpdateCommandHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
: IKyrolusCommandHandler<UpdateCommand<TResponse>, TResponse>
     where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<TResponse> Handle(UpdateCommand<TResponse> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        var entity = await repo.UpdateAsync(command.Entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity!;
    }
}
