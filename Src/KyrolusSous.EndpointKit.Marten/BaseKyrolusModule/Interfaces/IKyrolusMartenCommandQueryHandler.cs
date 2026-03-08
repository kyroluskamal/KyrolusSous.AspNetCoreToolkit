using KyrolusSous.EndpointKit.Core.Batch;
using KyrolusSous.Repositories.Marten.Abstractions.Query;

namespace KyrolusSous.EndpointKit.Marten.BaseKyrolusModule.Interfaces;

public interface IKyrolusMartenCommandQueryHandler<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
    Task<IResult> HandleQueryAsync([FromBody] QueryRequest? request, [FromQuery] bool? cacheable = null, [FromQuery] bool? includeDeleted = null, CancellationToken cancellationToken = default);
    Task<IResult> HandleGetAllPagedAsync([AsParameters] KyrolusMartenQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<IResult> HandleQueryPagedAsync([FromBody] KyrolusMartenPagedQueryRequest request, CancellationToken cancellationToken = default);
    Task<IResult> HandleSeekAsync([AsParameters] KyrolusMartenSeekQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<IResult> HandleQuerySeekAsync([FromBody] KyrolusMartenSeekQueryRequest request, CancellationToken cancellationToken = default);
    Task<IResult> HandleCountAsync([FromQuery] string? filter = null, [FromQuery] bool? includeDeleted = null, CancellationToken cancellationToken = default);
    Task<IResult> HandleHeadByIdAsync(TKey id, CancellationToken cancellationToken = default);
    Task<IResult> HandleBulkUpdateAsync([FromBody] KyrolusMartenBulkUpdateRequest request, CancellationToken cancellationToken = default);
    Task<IResult> HandleBulkDeleteAsync([FromBody] KyrolusMartenBulkDeleteRequest request, CancellationToken cancellationToken = default);
    Task<IResult> HandleBulkUpsertAsync([FromBody] IAsyncEnumerable<TModel> models, [FromQuery] bool? cacheable = null, CancellationToken cancellationToken = default);
    Task<IResult> HandleBulkPatchAsync([FromBody] IAsyncEnumerable<KyrolusMartenBulkPatchItem> items, [FromQuery] bool? cacheable = null, CancellationToken cancellationToken = default);
    Task<IResult> HandleGetByKeysAsync([FromQuery] string[]? keys, [FromQuery] string? includedProps = null, [FromQuery] string? includeGraph = null, [FromQuery] string? fields = null, [FromQuery] bool? cacheable = null, [FromQuery] bool? includeDeleted = null);
    Task<IResult> HandleUpdateByKeysAsync([FromQuery] string[]? keys, [FromBody] TModel model, [FromQuery] bool? cacheable = null);
    Task<IResult> HandleRemoveByKeysAsync([FromQuery] string[]? keys, [FromQuery] bool? cacheable = null);
    Task<IResult> HandlePatchByKeysAsync([FromQuery] string[]? keys, [FromBody] Dictionary<string, object> updates, [FromQuery] bool? cacheable = null);
    Task<IResult> HandleGetDeletedAsync([FromQuery] string? filter = null, [FromQuery] string? includedProps = null, [FromQuery] string? includeGraph = null, [FromQuery] string? fields = null, [FromQuery] bool? cacheable = null);
    Task<IResult> HandleRestoreAsync([FromRoute] TKey id, [FromQuery] bool? cacheable = null);
    Task<IResult> HandleRestoreByKeysAsync([FromQuery] string[]? keys, [FromQuery] bool? cacheable = null);
    Task<IResult> HandleBatchAsync([FromBody] KyrolusBatchRequest<TModel, TKey> request, CancellationToken cancellationToken = default);
}

