using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace KyrolusSous.CQRS.EF.Command.Bulk;

/// <summary>
/// Splits entities into inserts and updates by querying which keys already exist, then saves both in
/// one <c>SaveChangesAsync</c> call.
/// </summary>
/// <remarks>
/// This is a check-then-act upsert, not a database-native <c>INSERT ... ON CONFLICT</c>/<c>MERGE</c>:
/// the "does this key already exist" query and the eventual insert are not atomic. Two concurrent
/// upserts for the same new key can both see "not present" and both attempt to insert it - the
/// database's unique constraint (on whatever key column(s) <see cref="BulkUpsertCommand{TResponse, TKey}.KeyPropertyNames"/>
/// maps to) then rejects the later of the two writes, and this handler surfaces that as
/// <see cref="DbUpdateException"/> rather than silently converting it into an update. The whole
/// command still runs inside the enclosing transaction behavior's transaction, so a conflict here
/// rolls the entire command back cleanly - it cannot leave a partial write - but the caller does need
/// to be prepared to retry a genuinely concurrent upsert of the same key, the same way any unique
/// constraint violation would need retrying.
/// </remarks>
public sealed class BulkUpsertCommandHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
    : IKyrolusCommandHandler<BulkUpsertCommand<TResponse, TKey>, IEnumerable<TResponse>>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<IEnumerable<TResponse>> Handle(BulkUpsertCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        var entities = command.Entities?.Where(static e => e is not null).ToList() ?? [];
        if (entities.Count == 0) return entities;
        if (entities.Count > KyrolusBulkLimits.MaxBatchSize)
        {
            // The existence-check query below builds one OR-branch (with its own parameters) per
            // entity rather than a SQL IN(...), so SQL Server's ~2100-parameter-per-query ceiling
            // caps how many entities a single upsert can carry. Thrown, not clamped: silently
            // dropping entities from a bulk write would be data loss.
            throw new InvalidOperationException(
                $"[Kyrolus CQRS] Bulk upsert batch of {entities.Count} entities exceeds the maximum of " +
                $"{KyrolusBulkLimits.MaxBatchSize}. Split the batch into smaller chunks.");
        }
        if (command.KeyPropertyNames is null || command.KeyPropertyNames.Count == 0)
        {
            throw new InvalidOperationException("KeyPropertyNames is required for upsert.");
        }

        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        var keyProps = command.KeyPropertyNames;
        var existingKeys = await LoadExistingKeysAsync(repo, keyProps, entities, cancellationToken).ConfigureAwait(false);

        var toAdd = new List<TResponse>();
        var toUpdate = new List<TResponse>();
        foreach (var entity in entities)
        {
            var key = BuildKeyFingerprint(entity, keyProps);
            if (existingKeys.Contains(key))
            {
                toUpdate.Add(entity);
            }
            else
            {
                toAdd.Add(entity);
            }
        }

        if (toAdd.Count > 0)
        {
            await repo.AddRangeAsync(toAdd, cancellationToken).ConfigureAwait(false);
        }

        if (toUpdate.Count > 0)
        {
            await repo.UpdateRangeAsync(toUpdate, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (toAdd.Count > 0)
        {
            // Most likely cause, given this handler already checked "does this key exist" before
            // inserting: a concurrent upsert inserted the same key in between that check and this
            // save. Re-raised with an explicit message rather than left as a raw constraint-violation
            // exception, so this doesn't read as a generic/unexplained EF failure.
            throw new InvalidOperationException(
                "[Kyrolus CQRS] Bulk upsert failed to save - most likely because a concurrent upsert " +
                "inserted one of the same keys after this command checked for their existence. Retry " +
                "the command; the retry will see the now-existing rows and update them instead.",
                ex);
        }

        return entities;
    }

    private static async Task<HashSet<string>> LoadExistingKeysAsync(
        IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey> repo,
        IReadOnlyList<string> keyProps,
        IReadOnlyList<TResponse> entities,
        CancellationToken cancellationToken)
    {
        var uniqueEntities = entities
            .GroupBy(e => BuildKeyFingerprint(e, keyProps), StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        var filter = BuildKeyFilter(keyProps, uniqueEntities);
        if (filter is null) return new HashSet<string>(StringComparer.Ordinal);

        var existing = await repo.GetAllAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entity in existing)
        {
            set.Add(BuildKeyFingerprint(entity, keyProps));
        }

        return set;
    }

    private static Expression<Func<TResponse, bool>>? BuildKeyFilter(IReadOnlyList<string> keyProps, IReadOnlyList<TResponse> entities)
    {
        if (entities.Count == 0) return null;

        var parameter = Expression.Parameter(typeof(TResponse), "e");
        Expression? body = null;

        foreach (var entity in entities)
        {
            var values = ExtractKeyValues(entity, keyProps);
            Expression? predicate = null;
            for (var i = 0; i < keyProps.Count; i++)
            {
                var propName = keyProps[i];
                var property = typeof(TResponse).GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property is null)
                {
                    throw new InvalidOperationException($"Key property '{propName}' was not found on {typeof(TResponse).Name}.");
                }

                var member = Expression.Property(parameter, property);
                var value = values[i];
                var constant = Expression.Constant(ConvertKeyValue(value, property.PropertyType), property.PropertyType);
                var equal = Expression.Equal(member, constant);
                predicate = predicate is null ? equal : Expression.AndAlso(predicate, equal);
            }

            body = body is null ? predicate : Expression.OrElse(body, predicate!);
        }

        return body is null ? null : Expression.Lambda<Func<TResponse, bool>>(body, parameter);
    }

    private static object?[] ExtractKeyValues(TResponse entity, IReadOnlyList<string> keyProps)
    {
        var values = new object?[keyProps.Count];
        for (var i = 0; i < keyProps.Count; i++)
        {
            var propName = keyProps[i];
            var property = typeof(TResponse).GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null)
            {
                throw new InvalidOperationException($"Key property '{propName}' was not found on {typeof(TResponse).Name}.");
            }

            values[i] = property.GetValue(entity);
        }

        return values;
    }

    private static string BuildKeyFingerprint(TResponse entity, IReadOnlyList<string> keyProps)
    {
        var values = ExtractKeyValues(entity, keyProps);
        return string.Join("|", values.Select((v, i) => $"{i}={EscapeKeyPart(v)}"));
    }

    private static string EscapeKeyPart(object? value)
    {
        var s = value switch
        {
            null => "null",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? "null",
            _ => value.ToString() ?? "null"
        };
        return Uri.EscapeDataString(s);
    }

    private static object? ConvertKeyValue(object? value, Type targetType)
    {
        if (value is null) return null;
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying.IsInstanceOfType(value)) return value;
        if (value is IConvertible)
        {
            return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        }
        return value;
    }
}
