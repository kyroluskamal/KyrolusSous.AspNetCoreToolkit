using KyrolusSous.Repositories.EF.Abstractions.Helpers;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;

namespace KyrolusSous.CQRS.Marten.Query;

public class GetByKeyValuesQueryHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusQueryHandler<GetByKeyValuesQuery<TResponse, TKey>, TResponse?>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<TResponse?> Handle(GetByKeyValuesQuery<TResponse, TKey> query, CancellationToken cancellationToken)
    {
        var keyProps = ResolveKeyProperties(query);
        var filter = KyrolusEFRepositoryBase<TResponse>.GetPrimaryKeyFromKeyValues(query.KeyValues, keyProps);
        var options = BuildOptions(query, filter);

        if (query.IncludeDeleted)
        {
            var soft = TryResolveSoftRepository();
            if (soft is not null)
            {
                var items = await soft.GetAllIncludingDeletedAsync(options, cancellationToken).ConfigureAwait(false);
                return ApplyRowVersion(items.FirstOrDefault(), null, query.RowVersionPropertyName);
            }
        }

        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var result = await repo.GetAllAsync(options, cancellationToken).ConfigureAwait(false);
        return ApplyRowVersion(result.FirstOrDefault(), null, query.RowVersionPropertyName);
    }

    private static string[] ResolveKeyProperties(GetByKeyValuesQuery<TResponse, TKey> query)
    {
        if (query.KeyPropertyNames is { Count: > 0 })
        {
            return query.KeyPropertyNames.Where(static p => !string.IsNullOrWhiteSpace(p)).ToArray();
        }

        return ["Id"];
    }

    private static MartenQueryOptions<TResponse> BuildOptions(
        GetByKeyValuesQuery<TResponse, TKey> query,
        Expression<Func<TResponse, bool>> filter)
    {
        var mergedExpressions = MergeIncludeExpressions(query.IncludeExpressions, query.IncludeGraph);
        return new MartenQueryOptions<TResponse>(
            Filter: filter,
            IncludeProperties: query.IncludeProperties,
            IncludeExpressions: mergedExpressions,
            TenantId: query.TenantId,
            IncludeSoftDeleted: query.IncludeDeleted);
    }

    private static Expression<Func<TResponse, object?>>[]? MergeIncludeExpressions(
        Expression<Func<TResponse, object?>>[]? includes,
        IncludeGraph<TResponse>? graph)
    {
        if (includes is null && (graph?.Includes?.Count ?? 0) == 0) return null;
        var merged = new List<Expression<Func<TResponse, object?>>>();
        if (includes is not null) merged.AddRange(includes);
        if (graph?.Includes is not null) merged.AddRange(graph.Includes);
        return merged.Count == 0 ? null : merged.ToArray();
    }

    private static TResponse? ApplyRowVersion(TResponse? entity, Guid? version, string? rowVersionPropertyName)
    {
        if (entity is null) return null;
        if (!string.IsNullOrWhiteSpace(rowVersionPropertyName) && version.HasValue)
        {
            TrySetRowVersion(entity, rowVersionPropertyName, version.Value);
        }

        return entity;
    }

    private static void TrySetRowVersion(TResponse entity, string propertyName, Guid version)
    {
        var prop = typeof(TResponse).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop is null || !prop.CanWrite) return;

        if (prop.PropertyType == typeof(Guid) || prop.PropertyType == typeof(Guid?))
        {
            prop.SetValue(entity, version);
            return;
        }

        if (prop.PropertyType == typeof(string))
        {
            prop.SetValue(entity, version.ToString("N"));
            return;
        }

        if (prop.PropertyType == typeof(byte[]))
        {
            prop.SetValue(entity, version.ToByteArray());
        }
    }

    private IKyrolusMartenSoftDeleteRepositoryAsync<TSession, TResponse, TKey>? TryResolveSoftRepository()
    {
        try
        {
            return unitOfWork.GetRepository<IKyrolusMartenSoftDeleteRepositoryAsync<TSession, TResponse, TKey>>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
