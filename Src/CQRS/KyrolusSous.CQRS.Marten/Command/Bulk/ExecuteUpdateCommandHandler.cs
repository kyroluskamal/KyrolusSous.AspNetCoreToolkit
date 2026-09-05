namespace KyrolusSous.CQRS.Marten.Command.Bulk;

/// <summary>
/// Handles <see cref="ExecuteUpdateCommand{TResponse, TKey}"/> via the Marten repository's
/// PatchWhereAsync (the closest Marten idiom to EF's ExecuteUpdateAsync setter-list). Mirrors the
/// EF <c>ExecuteUpdateCommandHandler</c>.
/// </summary>
public sealed class ExecuteUpdateCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusCommandHandler<ExecuteUpdateCommand<TResponse, TKey>, int>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : notnull, IEquatable<TKey>
{
    public async Task<int> Handle(ExecuteUpdateCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Defense in depth: the command's constructor already rejects a null filter, and Filter is
        // init-only, but reflection (e.g. property-based command builders) can still bypass both, so
        // re-validate here before anything reaches the database.
        ArgumentNullException.ThrowIfNull(command.Filter, nameof(command.Filter));
        ArgumentNullException.ThrowIfNull(command.Updates);

        if (command.Updates.Count == 0)
        {
            return 0;
        }

        // Always-on, independent of IKyrolusPropertyUpdateRequest.AllowedProperties (which is opt-in
        // and does nothing when a caller never sets it): the document's identity and Marten's own
        // concurrency/revision tracking must never be writable through PatchWhereAsync regardless of
        // allow-list configuration. See MartenProtectedPropertyGuard, shared with Patch/BulkPatch.
        foreach (var name in command.Updates.Keys)
        {
            var prop = typeof(TResponse).GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is not null)
            {
                MartenProtectedPropertyGuard.ThrowIfProtected(prop, typeof(TResponse), "ExecuteUpdate");
            }
        }

        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var affected = await repo.PatchWhereAsync(command.Filter, command.Updates, command.TenantId, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return affected;
    }
}
