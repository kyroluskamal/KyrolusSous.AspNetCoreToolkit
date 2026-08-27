namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;

/// <summary>
/// Defines command and query handlers for CRUD operations in Kyrolus EndpointKit.
/// </summary>
public interface IKyrolusCommandQueryHandler<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
    Task<IResult> HandleGetAllAsync([FromQuery] string? filter = null, [FromQuery] string? includedProps = null, [FromQuery] string? includeGraph = null, [FromQuery] string? fields = null, [FromQuery] bool? cacheable = null, [FromQuery] bool? includeDeleted = null);
    Task<IResult> HandleGetByIdAsync(TKey id, [FromQuery] string? includedProps = null, [FromQuery] string? includeGraph = null, [FromQuery] string? fields = null, [FromQuery] bool? cacheable = null, [FromQuery] bool? includeDeleted = null);
    Task<IResult> HandleCreateAsync(TModel model, [FromQuery] bool? cacheable = null);
    Task<IResult> HandleCreateRangeAsync(IEnumerable<TModel> model, [FromQuery] bool? cacheable = null);
    Task<IResult> HandleUpdateAsync([FromRoute] TKey id, TModel model, [FromQuery] bool? cacheable = null);
    Task<IResult> HandleUpdateRangeAsync(IEnumerable<TModel> model, [FromQuery] bool? cacheable = null);
    Task<IResult> HandleRemoveAsync([FromRoute] TKey id, [FromQuery] bool? cacheable = null);
    Task<IResult> HandleRemoveRangeAsync([FromBody] IEnumerable<TModel> model, [FromQuery] bool? cacheable = null);
    Task<IResult> HandlePatchAsync([FromRoute] TKey id, [FromBody] Dictionary<string, object> updates, [FromQuery] bool? cacheable = null);
}

/// <summary>
/// Backward-compatibility alias for <see cref="IKyrolusCommandQueryHandler{TResponse, TModel, TKey}"/>.
/// </summary>
public interface ICommandQueryHandler<TResponse, TModel, TKey> : IKyrolusCommandQueryHandler<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
}
