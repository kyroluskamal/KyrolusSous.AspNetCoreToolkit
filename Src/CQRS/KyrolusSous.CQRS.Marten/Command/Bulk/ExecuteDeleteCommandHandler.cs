namespace KyrolusSous.CQRS.Marten.Command.Bulk;

/// <summary>
/// Handles <see cref="ExecuteDeleteCommand{TResponse, TKey}"/> via the Marten repository's
/// DeleteWhereAsync. Mirrors the EF <c>ExecuteDeleteCommandHandler</c>.
/// </summary>
public sealed class ExecuteDeleteCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusCommandHandler<ExecuteDeleteCommand<TResponse, TKey>, int>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : notnull, IEquatable<TKey>
{
    public async Task<int> Handle(ExecuteDeleteCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Defense in depth: the command's constructor already rejects a null filter, and Filter is
        // init-only, but reflection (e.g. property-based command builders) can still bypass both, so
        // re-validate here before anything reaches the database.
        ArgumentNullException.ThrowIfNull(command.Filter, nameof(command.Filter));

        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var affected = await repo.DeleteWhereAsync(command.Filter, command.TenantId, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return affected;
    }
}
