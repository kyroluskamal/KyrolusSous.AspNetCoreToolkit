namespace KyrolusSous.CQRS.EF.Command.Bulk;

public sealed class ExecuteDeleteCommandHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
    : IKyrolusCommandHandler<ExecuteDeleteCommand<TResponse, TKey>, int>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public Task<int> Handle(ExecuteDeleteCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        return repo.ExecuteDeleteAsync(command.Filter, command.UseSplitQuery, cancellationToken);
    }
}
