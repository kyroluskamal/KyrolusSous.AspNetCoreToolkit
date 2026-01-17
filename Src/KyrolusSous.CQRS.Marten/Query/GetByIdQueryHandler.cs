using KyrolusSous.Repositories.EF.Abstractions.Interfaces;

namespace KyrolusSous.CQRS.Marten.Query;

public class GetByIdQueryHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusQueryHandler<GetByIdQuery<TResponse, TKey>, TResponse?>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<TResponse?> Handle(GetByIdQuery<TResponse, TKey> query, CancellationToken cancellationToken)
    {
        var options = BuildOptions(query);
        if (query.IncludeDeleted)
        {
            var soft = TryResolveSoftRepository();
            if (soft is not null)
            {
                var result = await soft.GetByIdIncludingDeletedAsync(query.Id, options, cancellationToken).ConfigureAwait(false);
                return ApplyRowVersion(result, query.RowVersionPropertyName);
            }
        }

        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var item = await repo.GetByIdAsync(query.Id, options, cancellationToken).ConfigureAwait(false);
        return ApplyRowVersion(item, query.RowVersionPropertyName);
    }

    private static MartenQueryOptions<TResponse> BuildOptions(GetByIdQuery<TResponse, TKey> query)
    {
        var mergedExpressions = MergeIncludeExpressions(query.IncludeExpressions, query.IncludeGraph);
        return new MartenQueryOptions<TResponse>(
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

    private static TResponse? ApplyRowVersion(MartenEntityResult<TResponse>? result, string? rowVersionPropertyName)
    {
        if (result?.Entity is null) return null;
        if (!string.IsNullOrWhiteSpace(rowVersionPropertyName) && result.Version.HasValue)
        {
            TrySetRowVersion(result.Entity, rowVersionPropertyName, result.Version.Value);
        }

        return result.Entity;
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
