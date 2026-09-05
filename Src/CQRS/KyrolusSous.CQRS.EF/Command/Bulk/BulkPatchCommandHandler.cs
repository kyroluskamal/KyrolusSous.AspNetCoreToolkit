using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace KyrolusSous.CQRS.EF.Command.Bulk;

public sealed class BulkPatchCommandHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
    : IKyrolusCommandHandler<BulkPatchCommand<TResponse, TKey>, int>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<int> Handle(BulkPatchCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        var items = command.Items ?? [];
        if (items.Count == 0) return 0;
        if (items.Count > KyrolusBulkLimits.MaxBatchSize)
        {
            // Each item is its own PatchAsync round trip (not batched into a single SQL statement),
            // so an unbounded Items list means an unbounded number of sequential database round trips
            // inside one handler invocation - the same "silently dropping work is worse than rejecting
            // it" reasoning BulkUpsertCommandHandler documents for its own cap. Reuses that cap's value
            // rather than inventing a second, undocumented number for what is otherwise the same
            // "bulk write over TResponse" surface.
            throw new InvalidOperationException(
                $"[Kyrolus CQRS] Bulk patch batch of {items.Count} items exceeds the maximum of " +
                $"{KyrolusBulkLimits.MaxBatchSize}. Split the batch into smaller chunks.");
        }

        // Always-on, independent of IKyrolusPropertyUpdateRequest.AllowedProperties (which is opt-in
        // and does nothing when a caller never sets it): a key, concurrency-token, or DB-computed
        // column must never be writable through BulkPatch regardless of allow-list configuration.
        // Mirrors ExecuteUpdateCommandHandler/PatchCommandHandler's guard for the same
        // Dictionary<string, object> input shape - see EfProtectedPropertyGuard. Validated once up
        // front over every item's property names rather than per-item, so a bad batch is rejected
        // before any PatchAsync round trip runs.
        ValidateNoProtectedProperties(items);

        var keyLength = items[0].KeyValues.Length;
        if (keyLength <= 1)
        {
            var repo = unitOfWork.GetRepository<IKyrolusSingleKeyRepositoryAsync<TDbcontext, TResponse, TKey>>();
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = ConvertKey(item.KeyValues.Length > 0 ? item.KeyValues[0] : null);
                await repo.PatchAsync(key, item.Updates, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            var repo = unitOfWork.GetRepository<IKyrolusCompositeKeyRepositoryAsync<TDbcontext, TResponse, TKey>>();
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await repo.PatchAsync(item.KeyValues, item.Updates, cancellationToken).ConfigureAwait(false);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return items.Count;
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
                EfProtectedPropertyGuard.ThrowIfProtected(prop, typeof(TResponse), "BulkPatch");
            }
        }
    }

    private static TKey ConvertKey(object? raw)
    {
        if (raw is TKey typed) return typed;
        if (raw is null) throw new InvalidOperationException("Patch key is required.");
        var targetType = Nullable.GetUnderlyingType(typeof(TKey)) ?? typeof(TKey);
        return (TKey)Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
    }
}
