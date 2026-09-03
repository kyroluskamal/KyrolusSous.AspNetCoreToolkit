namespace KyrolusSous.CQRS.Marten.Command.SoftDelete;

/// <summary>
/// Handles <see cref="RestoreByIdCommand{TResponse, TKey}"/> against a Marten soft-delete
/// repository. Mirrors the EF <c>RestoreByIdCommandHandler</c>, but Marten documents only ever
/// have a single identity value (no composite keys), so exactly one key value is required.
/// </summary>
public sealed class RestoreByIdCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusCommandHandler<RestoreByIdCommand<TResponse, TKey>, bool>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<bool> Handle(RestoreByIdCommand<TResponse, TKey> command, CancellationToken cancellationToken)
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
        catch (InvalidOperationException ex) when (ex.IsRepositoryNotRegistered())
        {
            return false;
        }

        var restored = await repo.RestoreAsync(typedKey, command.TenantId, cancellationToken).ConfigureAwait(false);
        if (restored)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return restored;
    }
}
