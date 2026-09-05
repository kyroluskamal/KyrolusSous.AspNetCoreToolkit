namespace KyrolusSous.CQRS.Marten.Command.Bulk;

/// <summary>
/// Safety limit for CQRS Marten bulk commands that issue one repository round trip per item
/// (<see cref="BulkPatchCommandHandler{TSession, TResponse, TKey}"/>).
/// </summary>
public static class KyrolusBulkLimits
{
    /// <summary>
    /// Maximum items accepted per <c>BulkPatchCommand</c>. Marten's <c>PatchWhereAsync</c> has no
    /// single-statement, multi-key batch form the way EF's <c>ExecuteUpdate</c> does - each item is
    /// its own sequential round trip against the document store inside one handler invocation, so an
    /// unbounded Items list means an unbounded number of round trips before the command completes.
    /// Mirrors EF's own <c>KyrolusBulkLimits.MaxBatchSize</c> value and reasoning for the equivalent
    /// per-provider surface: thrown, not clamped, because silently dropping items from a bulk write
    /// would be data loss, not a safe default.
    /// </summary>
    public const int MaxBatchSize = 500;
}
