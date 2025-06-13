using KyrolusSous.CQRS.Base.Command.Add;
using KyrolusSous.CQRS.Base.Command.Patch;
using KyrolusSous.CQRS.Base.Command.Remove;
using KyrolusSous.CQRS.Base.Command.Update;
using KyrolusSous.CQRS.Base.Query;
using KyrolusSous.EasyAPI.BaseKyrolusModule.Enum;
using KyrolusSous.EasyAPI.BaseKyrolusModule.Interfaces;
using KyrolusSous.ExceptionHandling.ClasesAndHelpers;
using Newtonsoft.Json;

namespace KyrolusSous.EasyAPI.BaseKyrolusModule;

public class DefaultCommandQueryHandler<TResponse, TModel, TKey>(IKyrolusMapper kyrolusMapper,
 ISourceSender mediator, IKyrolusApiConfig<TResponse> config) : ICommandQueryHandler<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
    protected ISourceSender mediator = mediator;
    public async Task<IResult> HandleGetAllAsync([FromQuery] string? filter = null, [FromQuery] string? includedProps = null,
    [FromQuery] bool? cacheable = null)
    {
        var EndpointConfig = config.EndpointConfig.FirstOrDefault(x => x.Name == EndpointNames.GetAll);
        var getAllQuery = (GetAllQuery<TResponse>)(config.QueryAll ?? new GetAllQuery<TResponse>());
        if (cacheable.HasValue)
            getAllQuery.Cacheable = cacheable.Value;
        getAllQuery.IncludeProperties = KyrolusSousRoutingHelpers.GetIncludedProperties(includedProps);
        if (EndpointConfig != null)
        {
            getAllQuery.IncludeProperties?.AddRange(EndpointConfig.IncludeProps);
        }
        if (filter != null)
            getAllQuery.Filter = FilterBuilder.BuildFilterExpression<TResponse>(filter);
        var result = await mediator.SendAsync(getAllQuery);
        return Results.Ok(kyrolusMapper.MapResponseToViewModel(result, GetTheViewModelType(EndpointNames.GetAll), (int)HttpStatusCode.OK,
        $"Successfully retrieved {result.Count()} {typeof(TModel).Name}(s)"
        ));
    }
    public async Task<IResult> HandleGetByIdAsync([FromRoute] TKey id, [FromQuery] string? includedProps = null, [FromQuery] bool? cacheable = null)
    {
        var EndpointConfig = config.EndpointConfig.FirstOrDefault(x => x.Name == EndpointNames.GetById);
        var getByIdQuery = (GetByIdQuery<TResponse, TKey>)(config.QueryById ?? new GetByIdQuery<TResponse, TKey>(id, cacheable ?? false));
        getByIdQuery.IncludeProperties = KyrolusSousRoutingHelpers.GetIncludedProperties(includedProps);
        if (EndpointConfig != null)
            getByIdQuery.IncludeProperties?.AddRange(EndpointConfig.IncludeProps);
        if (cacheable.HasValue)
            getByIdQuery.Cacheable = cacheable.Value;
        dynamic result = await mediator.SendAsync(getByIdQuery);
        return Results.Ok(kyrolusMapper.MapResponseToViewModel(result, GetTheViewModelType(EndpointNames.GetById),
                         (int)HttpStatusCode.OK, $"Successfully retrieved {typeof(TModel).Name} with id {id}"));
    }
    public async Task<IResult> HandleCreateAsync([FromBody] TModel model, [FromQuery] bool? cacheable = null)
    {
        var entity = kyrolusMapper.MapModelToEntity<TModel, TResponse>(model);
        var command = (AddCommand<TResponse>)config.AddCommand
        ?? new AddCommand<TResponse>(entity);
        command.Entity = entity;
        if (cacheable.HasValue)
            command.Cacheable = cacheable.Value;
        var result = await mediator.SendAsync(command);

        return Results.Created($"/{config.Prefix}/{config.Route}/{result.GetType().GetProperty("id")}",
        kyrolusMapper.MapResponseToViewModel(result,
            GetTheViewModelType(EndpointNames.Add), (int)HttpStatusCode.Created,
        $"Successfully created the {typeof(TModel).Name}"
        ));
    }
    public async Task<IResult> HandleCreateRangeAsync([FromBody] IEnumerable<TModel> model, [FromQuery] bool? cacheable = null)
    {
        var entities = kyrolusMapper.MapModelToEntity<TModel, TResponse>(model);
        var command = (AddRangeCommand<TResponse>)config.AddRangeCommand ?? new AddRangeCommand<TResponse>(entities);
        if (cacheable.HasValue)
            command.Cacheable = cacheable.Value;
        command.Entities = entities;
        var result = await mediator.SendAsync(command);
        return Results.CreatedAtRoute(kyrolusMapper.MapResponseToViewModel(result,
                                GetTheViewModelType(EndpointNames.AddRange),
                                (int)HttpStatusCode.Created, $"Successfully created {typeof(TModel).Name} records"));
    }
    public async Task<IResult> HandleUpdateAsync([FromBody] TModel model, [FromQuery] bool? cacheable = null)
    {
        var entity = kyrolusMapper.MapModelToEntity<TModel, TResponse>(model);
        var command = (UpdateCommand<TResponse>)config.UpdateCommand ?? new UpdateCommand<TResponse>(entity);
        if (cacheable.HasValue)
            command.Cacheable = cacheable.Value;
        command.Entity = entity;
        var result = await mediator.SendAsync(command);
        return Results.Ok(kyrolusMapper.MapResponseToViewModel(result,
        GetTheViewModelType(EndpointNames.Update), (int)HttpStatusCode.OK,
                $"Successfully updated {typeof(TModel).Name} with id {result.GetType().GetProperty("id")}"
                ));
    }
    public async Task<IResult> HandleUpdateRangeAsync([FromBody] IEnumerable<TModel> model, [FromQuery] bool? cacheable = null)
    {
        var entities = kyrolusMapper.MapModelToEntity<TModel, TResponse>(model);
        var command = (UpdateRangeCommand<TResponse>)config.UpdateRangeCommand ?? new UpdateRangeCommand<TResponse>(entities);
        if (cacheable.HasValue)
            command.Cacheable = cacheable.Value;
        command.Entities = entities;

        var result = await mediator.SendAsync(command);
        return Results.Ok(kyrolusMapper.MapResponseToViewModel(result,
        GetTheViewModelType(EndpointNames.UpdateRange)
        , (int)HttpStatusCode.OK,
                $"Successfully updated {typeof(TModel).Name} records"
                ));
    }
    public async Task<IResult> HandleRemoveAsync([FromRoute] TKey id, [FromQuery] bool? cacheable = null, [FromQuery] string? compositeKey = null)
    {
        object[] keyArray = [];
        if (compositeKey != null)
        {
            keyArray = JsonConvert.DeserializeObject<object[]>(compositeKey) ?? [];
        }
        if (keyArray.Length == 0)
        {
            keyArray = [id];
        }
        var command = (RemoveByIdCommand<TResponse, TKey>)config.RemoveCommand ?? new RemoveByIdCommand<TResponse, TKey>(keyArray, cacheable ?? false);
        command.KeyValues = keyArray;
        if (cacheable.HasValue)
            command.Cacheable = cacheable.Value;
        await mediator.SendAsync(command);
        return Results.Ok(config.UseEnrichedCustomResponse ? new Response((int)HttpStatusCode.OK,
         $"Successfully deleted {typeof(TModel).Name} with id {string.Join(",", keyArray)}", true, new { id }) : new { id });
    }
    public async Task<IResult> HandleRemoveRangeAsync([FromBody] RemoveRangeCommand<TResponse> command)
    {
        await mediator.SendAsync(command);
        return Results.Ok(config.UseEnrichedCustomResponse ?
                         new Response((int)HttpStatusCode.OK, $"Successfully deleted {typeof(TModel).Name} records", true, command.Entities) : command.Entities);
    }

    private Type GetTheViewModelType(EndpointNames endpointName)
    {
        var endpointConfig = config.EndpointConfig.FirstOrDefault(x => x.Name == endpointName);
        return endpointConfig?.ViewModelType == null ? config.ViewModelType : endpointConfig.ViewModelType;
    }

    public async Task<IResult> HandlePatchAsync([FromRoute] TKey id, [FromBody] Dictionary<string, object> updates, [FromQuery] bool? cacheable = null, [FromQuery] string? compositeKey = null)
    {
        var command = (PatchCommand<TResponse, TKey>)config.PatchCommand ?? new PatchCommand<TResponse, TKey>([id], updates, cacheable ?? false);
        command.KeyValues ??= [id];
        if (compositeKey != null)
            command.KeyValues = JsonConvert.DeserializeObject<object[]>(compositeKey) ?? [id];
        if (cacheable.HasValue)
            command.Cacheable = cacheable.Value;
        command.Updates = updates;
        var result = await mediator.SendAsync(command);
        return Results.Ok(config.UseEnrichedCustomResponse ?
                         new Response((int)HttpStatusCode.OK, $"Successfully patched {typeof(TModel).Name}", true, result) : result);
    }
}
