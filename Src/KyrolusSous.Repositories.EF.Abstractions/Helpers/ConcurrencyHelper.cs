namespace KyrolusSous.Repositories.EF.Abstractions.Helpers;

public static class ConcurrencyHelper
{
    public static async Task<ConcurrencyInfo?> BuildConcurrencyInfoAsync(DbUpdateConcurrencyException ex, string? rowVersionPropertyName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ex);
        var entry = ex.Entries.Count > 0 ? ex.Entries[0] : null;
        if (entry is null) return null;

        byte[]? original = null;
        byte[]? current = null;
        IReadOnlyDictionary<string, object?>? databaseValues = null;

        try
        {
            var dbValues = await entry.GetDatabaseValuesAsync(cancellationToken).ConfigureAwait(false);
            if (dbValues is not null)
            {
                databaseValues = dbValues.Properties.ToDictionary(p => p.Name, p => dbValues[p]);
                if (!string.IsNullOrWhiteSpace(rowVersionPropertyName) && dbValues.TryGetValue<object>(rowVersionPropertyName!, out var rvObj) && rvObj is byte[] dbRv)
                {
                    current = dbRv;
                }
            }
        }
        catch
        {
            // ignore if cannot retrieve database values
        }

        if (!string.IsNullOrWhiteSpace(rowVersionPropertyName))
        {
            try
            {
                var prop = entry.Property(rowVersionPropertyName!);
                if (prop?.OriginalValue is byte[] ov) original = ov;
            }
            catch
            {
                // ignore if not present
            }
        }

        return new ConcurrencyInfo(original, current, databaseValues);
    }

    public static async Task<RepositoryOperationResult<TResult>> ExecuteWithConcurrencyRetryAsync<TResult>(
        Func<Task<TResult>> action,
        KyrolusRepositoryPolicy policy,
        Func<DbUpdateConcurrencyException, Task<ConcurrencyInfo?>>? concurrencyInfoFactory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(policy);

        for (var attempt = 0; attempt <= policy.ConcurrencyRetryCount; attempt++)
        {
            try
            {
                var result = await action().ConfigureAwait(false);
                return RepositoryOperationResult<TResult>.Success(result);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                var enriched = await ResolveConcurrencyInfoAsync(concurrencyInfoFactory, ex, attempt, cancellationToken).ConfigureAwait(false);
                if (attempt == policy.ConcurrencyRetryCount)
                    return RepositoryOperationResult<TResult>.ConcurrencyConflict(ex, enriched);

                await DelayIfNeededAsync(policy.ConcurrencyRetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return RepositoryOperationResult<TResult>.Failed(ex);
            }
        }

        // Should never be hit, but keeps compiler satisfied in case of future changes.
        return RepositoryOperationResult<TResult>.ConcurrencyConflict(
            new DbUpdateConcurrencyException("Maximum concurrency retries reached."),
            new ConcurrencyInfo(null, null, null, policy.ConcurrencyRetryCount));
    }

    private static async Task<ConcurrencyInfo> ResolveConcurrencyInfoAsync(
        Func<DbUpdateConcurrencyException, Task<ConcurrencyInfo?>>? concurrencyInfoFactory,
        DbUpdateConcurrencyException ex,

        int attempt, CancellationToken cancellationToken)
    {
        var info = concurrencyInfoFactory != null
            ? await concurrencyInfoFactory(ex).ConfigureAwait(false)
            : await BuildConcurrencyInfoAsync(ex, null, cancellationToken).ConfigureAwait(false);

        return info is null
            ? new ConcurrencyInfo(null, null, null, attempt)
            : new ConcurrencyInfo(info.Value.OriginalRowVersion, info.Value.CurrentRowVersion, info.Value.DatabaseValues, attempt);
    }

    private static Task DelayIfNeededAsync(TimeSpan? delay, CancellationToken cancellationToken)
    {
        if (delay.HasValue && delay.Value > TimeSpan.Zero)
        {
            return Task.Delay(delay.Value, cancellationToken);
        }
        return Task.CompletedTask;
    }
}
