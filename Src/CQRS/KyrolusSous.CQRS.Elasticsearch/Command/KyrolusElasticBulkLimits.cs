namespace KyrolusSous.CQRS.Elasticsearch.Command;

/// <summary>
/// Safety limit for CQRS Elasticsearch bulk commands (<see cref="ElasticBulkIndexCommandHandler{TDocument, TId}"/>,
/// <see cref="ElasticBulkDeleteCommandHandler{TDocument, TId}"/>).
/// </summary>
public static class KyrolusElasticBulkLimits
{
    /// <summary>
    /// Maximum items accepted per bulk Elasticsearch command. Unlike EF's <c>KyrolusBulkLimits.MaxBatchSize</c>
    /// (bounded by SQL Server's ~2100-parameter-per-query ceiling), Elasticsearch's <c>_bulk</c> API has no
    /// equivalent hard parameter-count wall - a single request can technically carry far more actions than
    /// this. The risk here is different: one caller submitting an unbounded batch in a single request
    /// monopolizes the cluster's bulk thread pool queue (a fixed-size queue shared by every index/update/
    /// delete action across the whole cluster, not just this caller) and inflates the HTTP request payload
    /// past what a coordinating node comfortably buffers before rejecting it - both of which turn one
    /// oversized request into a cluster-wide availability problem for other callers, not merely a slow
    /// response for this one. 1000 mirrors this library's own default bulk batch size
    /// (<c>KyrolusElasticsearchOptions.BulkBatchSize</c>), so a caller who already batches to that size is
    /// unaffected; a caller who doesn't gets an explicit rejection instead of an unbounded request quietly
    /// stressing the cluster. Thrown, not clamped: silently dropping items from a bulk write would be data
    /// loss, not a safe default - the same reasoning EF's <c>KyrolusBulkLimits</c> documents for its own limit.
    /// </summary>
    public const int MaxBatchSize = 1000;
}
