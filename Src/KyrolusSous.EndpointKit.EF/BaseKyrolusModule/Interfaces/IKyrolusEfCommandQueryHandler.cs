namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule.Interfaces;

public interface IKyrolusEfCommandQueryHandler<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
    Task<IResult> HandleQueryAsync([FromBody] QueryRequest? request, [FromQuery] bool? cacheable = null, CancellationToken cancellationToken = default);
    Task<IResult> HandleGetAllPagedAsync([AsParameters] KyrolusEfQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<IResult> HandleQueryPagedAsync([FromBody] KyrolusEfPagedQueryRequest request, CancellationToken cancellationToken = default);
    Task<IResult> HandleBulkUpdateAsync([FromBody] KyrolusEfBulkUpdateRequest request, CancellationToken cancellationToken = default);
    Task<IResult> HandleBulkDeleteAsync([FromBody] KyrolusEfBulkDeleteRequest request, CancellationToken cancellationToken = default);
    Task<IResult> HandleGetByKeysAsync([FromQuery] string[]? keys, [FromQuery] string? includedProps = null, [FromQuery] string? fields = null, [FromQuery] bool? cacheable = null);
    Task<IResult> HandleUpdateByKeysAsync([FromQuery] string[]? keys, [FromBody] TModel model, [FromQuery] bool? cacheable = null);
    Task<IResult> HandleRemoveByKeysAsync([FromQuery] string[]? keys, [FromQuery] bool? cacheable = null);
    Task<IResult> HandlePatchByKeysAsync([FromQuery] string[]? keys, [FromBody] Dictionary<string, object> updates, [FromQuery] bool? cacheable = null);
}
