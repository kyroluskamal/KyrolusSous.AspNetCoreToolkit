using KyrolusSous.Repositories.Marten.Abstractions.Query;

namespace KyrolusSous.CQRS.Marten.Command.Bulk;

public sealed class BulkPatchCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusCommandHandler<BulkPatchCommand<TResponse, TKey>, int>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<int> Handle(BulkPatchCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        var items = command.Items ?? [];
        if (items.Count == 0) return 0;
        if (items.Count > KyrolusBulkLimits.MaxBatchSize)
        {
            // Each item is its own PatchWhereAsync round trip (Marten has no single-statement,
            // multi-key batch patch), so an unbounded Items list means an unbounded number of
            // sequential round trips inside one handler invocation. Mirrors EF's BulkPatchCommandHandler
            // cap and reasoning for the same "bulk write over TResponse" surface.
            throw new InvalidOperationException(
                $"[Kyrolus CQRS] Bulk patch batch of {items.Count} items exceeds the maximum of " +
                $"{KyrolusBulkLimits.MaxBatchSize}. Split the batch into smaller chunks.");
        }

        // Always-on, independent of IKyrolusPropertyUpdateRequest.AllowedProperties (which is opt-in
        // and does nothing when a caller never sets it): the document's identity and Marten's own
        // concurrency/revision tracking must never be writable through BulkPatch regardless of
        // allow-list configuration. Mirrors ExecuteUpdateCommandHandler/PatchCommandHandler's guard
        // for the same Dictionary<string, object> input shape - see MartenProtectedPropertyGuard.
        // Validated once up front over every item's property names rather than per-item, so a bad
        // batch is rejected before any PatchWhereAsync round trip runs.
        ValidateNoProtectedProperties(items);

        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var keyProps = ResolveKeyProperties(command);
        var total = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filter = KyrolusQueryExpressionBuilder<TResponse>.GetPrimaryKeyFromKeyValues(item.KeyValues, keyProps);
            total += await repo.PatchWhereAsync(filter, item.Updates, command.TenantId, cancellationToken).ConfigureAwait(false);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return total;
    }

    private static void ValidateNoProtectedProperties(IReadOnlyList<KyrolusBulkPatchItem> items)
    {
        var propertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            foreach (var name in item.Updates.Keys)
            {
                propertyNames.Add(name);
            }
        }

        foreach (var name in propertyNames)
        {
            var prop = typeof(TResponse).GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is not null)
            {
                MartenProtectedPropertyGuard.ThrowIfProtected(prop, typeof(TResponse), "BulkPatch");
            }
        }
    }

    private static string[] ResolveKeyProperties(BulkPatchCommand<TResponse, TKey> command)
    {
        if (command.KeyPropertyNames is { Count: > 0 })
        {
            return command.KeyPropertyNames.Where(static p => !string.IsNullOrWhiteSpace(p)).ToArray();
        }

        return ["Id"];
    }
}

