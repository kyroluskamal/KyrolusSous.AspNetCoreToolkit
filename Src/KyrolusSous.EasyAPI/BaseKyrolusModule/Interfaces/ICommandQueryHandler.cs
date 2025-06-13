using KyrolusSous.CQRS.Base.Command.Remove;

namespace KyrolusSous.EasyAPI.BaseKyrolusModule.Interfaces;

public interface ICommandQueryHandler<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
    public Task<IResult> HandleGetAllAsync([FromQuery] string? filter = null, [FromQuery] string? includedProps = null, [FromQuery] bool? cacheable = null);
    public Task<IResult> HandleGetByIdAsync(TKey id, [FromQuery] string? includedProps = null, [FromQuery] bool? cacheable = null);
    public Task<IResult> HandleCreateAsync(TModel model, [FromQuery] bool? cacheable = null);
    public Task<IResult> HandleCreateRangeAsync(IEnumerable<TModel> model, [FromQuery] bool? cacheable = null);
    public Task<IResult> HandleUpdateAsync(TModel model, [FromQuery] bool? cacheable = null);
    public Task<IResult> HandleUpdateRangeAsync(IEnumerable<TModel> model, [FromQuery] bool? cacheable = null);
    // ActivationStateAsync
    public Task<IResult> HandleRemoveAsync([FromRoute] TKey id, [FromQuery] bool? cacheable = null, [FromQuery] string? compositeKey = null);
    public Task<IResult> HandleRemoveRangeAsync([FromBody] RemoveRangeCommand<TResponse> command);
    public Task<IResult> HandlePatchAsync([FromRoute] TKey id, [FromBody] Dictionary<string, object> updates, [FromQuery] bool? cacheable = null, [FromQuery] string? compositeKey = null);
}