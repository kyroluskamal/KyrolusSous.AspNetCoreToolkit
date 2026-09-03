namespace KyrolusSous.CQRS.EF.Command.Bulk;

public sealed class ExecuteDeleteCommandHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
    : IKyrolusCommandHandler<ExecuteDeleteCommand<TResponse, TKey>, int>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public Task<int> Handle(ExecuteDeleteCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Defense in depth: the command's constructor already rejects a null filter, and Filter is
        // init-only, but reflection (e.g. property-based command builders) can still bypass both, so
        // re-validate here before anything reaches the database.
        ArgumentNullException.ThrowIfNull(command.Filter, nameof(command.Filter));
        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        return repo.ExecuteDeleteAsync(command.Filter, command.UseSplitQuery, cancellationToken);
    }
}
