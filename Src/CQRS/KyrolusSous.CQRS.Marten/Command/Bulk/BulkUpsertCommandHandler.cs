namespace KyrolusSous.CQRS.Marten.Command.Bulk;

/// <remarks>
/// Marten's <see cref="IKyrolusMartenRepositoryAsync{TSession, TEntity, TKey}.UpsertRangeAsync"/> has
/// no <c>keyPropertyNames</c> parameter - unlike EF's check-then-act upsert (which can key the
/// existence check on any column(s)), Marten's <c>session.Store</c> always upserts by the document's
/// own identity (its <c>Id</c> property, or whatever <see cref="JasperFx.IdentityAttribute"/> marks -
/// see the Marten <c>ExecuteUpdateCommandHandler.IsProtectedFromUpdate</c> remark for the same
/// convention). <see cref="BulkUpsertCommand{TResponse, TKey}.KeyPropertyNames"/> therefore cannot be
/// honored as an arbitrary key for this provider: rather than silently ignoring it (which would
/// mislead a caller into thinking a non-Id key was actually used to decide insert-vs-update), this
/// handler validates it is either unset or exactly <c>["Id"]</c> and throws otherwise.
/// </remarks>
public sealed class BulkUpsertCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusCommandHandler<BulkUpsertCommand<TResponse, TKey>, IEnumerable<TResponse>>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<IEnumerable<TResponse>> Handle(BulkUpsertCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        var entities = command.Entities?.Where(static e => e is not null).ToList() ?? [];
        if (entities.Count == 0) return entities;
        if (entities.Count > KyrolusBulkLimits.MaxBatchSize)
        {
            // Mirrors EF's BulkUpsertCommandHandler cap and reasoning: thrown, not clamped, because
            // silently dropping entities from a bulk write would be data loss, not a safe default.
            throw new InvalidOperationException(
                $"[Kyrolus CQRS] Bulk upsert batch of {entities.Count} entities exceeds the maximum of " +
                $"{KyrolusBulkLimits.MaxBatchSize}. Split the batch into smaller chunks.");
        }

        if (command.KeyPropertyNames is { Count: > 0 } keyPropertyNames &&
            !(keyPropertyNames.Count == 1 && string.Equals(keyPropertyNames[0], "Id", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "[Kyrolus CQRS] Marten's BulkUpsertCommandHandler always upserts by document Id - " +
                $"KeyPropertyNames must be unset or exactly [\"Id\"], but was [{string.Join(", ", keyPropertyNames)}]. " +
                "Marten has no equivalent of EF's arbitrary-key existence check.");
        }

        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var result = await repo.UpsertRangeAsync(entities, command.TenantId, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
