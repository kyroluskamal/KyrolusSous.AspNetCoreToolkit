namespace KyrolusSous.CQRS.EF.Command.Bulk;

/// <summary>
/// Safety limit for EF Core bulk commands that build one predicate branch per entity, such as
/// <see cref="BulkUpsertCommandHandler{TDbcontext, TResponse, TKey}"/>'s existence-check query.
/// </summary>
public static class KyrolusBulkLimits
{
    /// <summary>
    /// Maximum entities accepted per <c>BulkUpsertCommand</c>. The existence-check query adds one
    /// SQL parameter per key column per entity (it's a chain of OR'd equality branches, not an
    /// <c>IN(...)</c>), and SQL Server rejects a query with more than ~2100 parameters. Unlike
    /// PageSize (see <c>KyrolusPagingLimits.MaxPageSize</c>), exceeding this throws instead of
    /// silently truncating the batch - silently dropping entities from a bulk write would be data
    /// loss, not a safe default.
    /// </summary>
    public const int MaxBatchSize = 500;
}
