namespace KyrolusSous.Repositories.EF.Abstractions.Temporal;

/// <summary>
/// Provides temporal (time-travel) querying operations over entities mapped to SQL Server or PostgreSQL system-versioned temporal tables.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IKyrolusTemporalRepository<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Queries the historical state of the entity table as of a specific UTC point in time.
    /// </summary>
    Task<IEnumerable<TEntity>> GetAllAsOfAsync(
        DateTime utcPointInTime,
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries historical records that were active between <paramref name="utcFrom"/> and <paramref name="utcTo"/>.
    /// </summary>
    Task<IEnumerable<TEntity>> GetAllBetweenAsync(
        DateTime utcFrom,
        DateTime utcTo,
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries historical records whose entire validity period was contained within <paramref name="utcFrom"/> and <paramref name="utcTo"/>.
    /// </summary>
    Task<IEnumerable<TEntity>> GetAllContainedInAsync(
        DateTime utcFrom,
        DateTime utcTo,
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries all historical and current versions across the entire lifecycle of the table.
    /// </summary>
    Task<IEnumerable<TEntity>> GetAllVersionsAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        CancellationToken cancellationToken = default);
}
