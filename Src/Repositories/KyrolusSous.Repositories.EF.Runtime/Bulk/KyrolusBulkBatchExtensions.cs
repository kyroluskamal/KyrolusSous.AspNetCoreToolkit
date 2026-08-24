namespace KyrolusSous.Repositories.EF.Runtime.Bulk;

/// <summary>
/// Provides high-speed bulk/batch update and delete extensions that execute directly on the database in a single roundtrip.
/// </summary>
public static class KyrolusBulkBatchExtensions
{
    /// <summary>
    /// Executes a database-side batch delete directly on records matching the specified query without loading entities into memory.
    /// </summary>
    public static Task<int> ExecuteBatchDeleteAsync<TEntity>(
        this IQueryable<TEntity> source,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);

        return EntityFrameworkQueryableExtensions.ExecuteDeleteAsync(source, cancellationToken);
    }

    /// <summary>
    /// Executes a database-side batch delete directly on records matching the specified predicate.
    /// </summary>
    public static Task<int> ExecuteBatchDeleteAsync<TEntity>(
        this DbSet<TEntity> set,
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(predicate);

        return EntityFrameworkQueryableExtensions.ExecuteDeleteAsync(set.Where(predicate), cancellationToken);
    }
}
