using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace KyrolusSous.CQRS.EF.Command.Bulk;

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

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
