namespace KyrolusSous.CQRS.Marten.Command.SoftDelete;

/// <summary>
/// Handles <see cref="SoftDeleteByIdCommand{TResponse, TKey}"/> against a Marten soft-delete
/// repository. Mirrors the EF <c>SoftDeleteByIdCommandHandler</c>, but Marten documents only ever
/// have a single identity value (no composite keys), so exactly one key value is required.
/// </summary>
public sealed class SoftDeleteByIdCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusCommandHandler<SoftDeleteByIdCommand<TResponse, TKey>, bool>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<bool> Handle(SoftDeleteByIdCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        var keyValues = command.KeyValues;
        if (keyValues is null || keyValues.Length != 1)
        {
            throw new ArgumentException("Marten documents use a single key value.", nameof(command));
        }

        if (keyValues[0] is not TKey typedKey)
        {
            throw new ArgumentException("Key value type mismatch.", nameof(command));
        }

        IKyrolusMartenSoftDeleteRepositoryAsync<TSession, TResponse, TKey> repo;
        try
        {
            repo = unitOfWork.GetRepository<IKyrolusMartenSoftDeleteRepositoryAsync<TSession, TResponse, TKey>>();
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        // The soft-delete repository's RemoveAsync(TKey, ...) flips the soft-delete flag instead of
        // physically deleting the document when the soft-delete policy is enabled for TResponse.
        var removed = await repo.RemoveAsync(typedKey, expectedVersion: null, tenantId: command.TenantId, cancellationToken).ConfigureAwait(false);
        if (removed)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return removed;
    }
}
