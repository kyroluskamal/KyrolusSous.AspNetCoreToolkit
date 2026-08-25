using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;

namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

public class DefaultRouteMapper<TResponse, TModel, TKey> : IRouteMapper<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
    public RouteGroupBuilder MapEndpoints(IEndpointRouteBuilder app, IKyrolusApiConfig<TResponse> config)
    {
        config.Route ??= typeof(TResponse).Name;
        config.ApiName ??= typeof(TResponse).Name;
        var groupPrefix = BuildGroupPrefix(config);
        var group = app.MapGroup(groupPrefix).WithTags(config.ApiName);
        var resource = $"{config.Route}s";
        var endpointsToMap = GetEndpointsToMap(config);
        var useExclusions = config.AllEndpointsExcept is not null && config.AllEndpointsExcept.Any();
        bool ShouldMap(EndpointNames currentEndpoint) => useExclusions ?
            !endpointsToMap.Contains(currentEndpoint) : endpointsToMap.Contains(currentEndpoint)
            || endpointsToMap.Contains(EndpointNames.All);

        if (ShouldMap(EndpointNames.GetAll))
        {
            group.MapGet($"{resource}",
                ([FromServices] ICommandQueryHandler<TResponse, TModel, TKey> handler,
                    [FromQuery] string? filter,
                    [FromQuery] string? includedProps,
                    [FromQuery] string? includeGraph,
                    [FromQuery] string? fields,
                    [FromQuery] bool? cacheable,
                    [FromQuery] bool? includeDeleted) =>
                    handler.HandleGetAllAsync(filter, includedProps, includeGraph, fields, cacheable, includeDeleted))
                .Authorize(Authorize(config, EndpointNames.GetAll))
                .ApplyOpenApi(config, EndpointNames.GetAll)
                .ApplyEndpointPolicies(config, EndpointNames.GetAll);
        }

        if (ShouldMap(EndpointNames.GetById))
        {
            group.MapGet($"{resource}/{{id}}",
                ([FromServices] ICommandQueryHandler<TResponse, TModel, TKey> handler,
                    [FromRoute] TKey id,
                    [FromQuery] string? includedProps,
                    [FromQuery] string? includeGraph,
                    [FromQuery] string? fields,
                    [FromQuery] bool? cacheable,
                    [FromQuery] bool? includeDeleted) =>
                    handler.HandleGetByIdAsync(id, includedProps, includeGraph, fields, cacheable, includeDeleted))
                .Authorize(Authorize(config, EndpointNames.GetById))
                .ApplyOpenApi(config, EndpointNames.GetById)
                .ApplyEndpointPolicies(config, EndpointNames.GetById);
        }

        if (ShouldMap(EndpointNames.Add) || ShouldMap(EndpointNames.AddRange))
        {
            group.MapPost($"{resource}",
                async ([FromServices] ICommandQueryHandler<TResponse, TModel, TKey> handler,
                    [FromServices] IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions,
                    [FromBody] JsonElement body,
                    [FromQuery] bool? cacheable) =>
                {
                    var options = jsonOptions.Value.SerializerOptions;
                    if (body.ValueKind == JsonValueKind.Array)
                    {
                        var models = body.Deserialize<IEnumerable<TModel>>(options);
                        if (models is null) return Results.BadRequest("Invalid payload.");
                        return await handler.HandleCreateRangeAsync(models, cacheable);
                    }

                    var model = body.Deserialize<TModel>(options);
                    if (model is null) return Results.BadRequest("Invalid payload.");
                    return await handler.HandleCreateAsync(model, cacheable);
                })
                .Authorize(Authorize(config, EndpointNames.Add))
                .ApplyOpenApi(config, EndpointNames.Add)
                .ApplyEndpointPolicies(config, EndpointNames.Add);
        }

        if (ShouldMap(EndpointNames.Update))
        {
            group.MapPut($"{resource}/{{id}}",
                ([FromServices] ICommandQueryHandler<TResponse, TModel, TKey> handler,
                    [FromRoute] TKey id,
                    [FromBody] TModel model,
                    [FromQuery] bool? cacheable) =>
                    handler.HandleUpdateAsync(id, model, cacheable))
                .Authorize(Authorize(config, EndpointNames.Update))
                .ApplyOpenApi(config, EndpointNames.Update)
                .ApplyEndpointPolicies(config, EndpointNames.Update);
        }

        if (ShouldMap(EndpointNames.Patch))
        {
            group.MapPatch($"{resource}/{{id}}",
                ([FromServices] ICommandQueryHandler<TResponse, TModel, TKey> handler,
                    [FromRoute] TKey id,
                    [FromBody] Dictionary<string, object> updates,
                    [FromQuery] bool? cacheable) =>
                    handler.HandlePatchAsync(id, updates, cacheable))
                .Authorize(Authorize(config, EndpointNames.Patch))
                .ApplyOpenApi(config, EndpointNames.Patch)
                .ApplyEndpointPolicies(config, EndpointNames.Patch);
        }

        if (ShouldMap(EndpointNames.UpdateRange))
        {
            group.MapPut($"{resource}",
                async ([FromServices] ICommandQueryHandler<TResponse, TModel, TKey> handler,
                    [FromServices] IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions,
                    [FromBody] JsonElement body,
                    [FromQuery] bool? cacheable) =>
                {
                    var options = jsonOptions.Value.SerializerOptions;
                    if (body.ValueKind != JsonValueKind.Array)
                        return Results.BadRequest("Bulk update expects an array payload.");

                    var models = body.Deserialize<IEnumerable<TModel>>(options);
                    if (models is null) return Results.BadRequest("Invalid payload.");
                    return await handler.HandleUpdateRangeAsync(models, cacheable);
                })
                .Authorize(Authorize(config, EndpointNames.UpdateRange))
                .ApplyOpenApi(config, EndpointNames.UpdateRange)
                .ApplyEndpointPolicies(config, EndpointNames.UpdateRange);
        }

        if (ShouldMap(EndpointNames.Delete))
        {
            group.MapDelete($"{resource}/{{id}}",
                ([FromServices] ICommandQueryHandler<TResponse, TModel, TKey> handler,
                    [FromRoute] TKey id,
                    [FromQuery] bool? cacheable) =>
                    handler.HandleRemoveAsync(id, cacheable))
                .Authorize(Authorize(config, EndpointNames.Delete))
                .ApplyOpenApi(config, EndpointNames.Delete)
                .ApplyEndpointPolicies(config, EndpointNames.Delete);
        }

        if (ShouldMap(EndpointNames.DeleteRange))
        {
            group.MapDelete($"{resource}",
                async ([FromServices] ICommandQueryHandler<TResponse, TModel, TKey> handler,
                    [FromQuery] TKey[]? ids,
                    [FromQuery] bool? cacheable) =>
                {
                    if (ids is null || ids.Length == 0)
                        return Results.BadRequest("Bulk delete requires ids query parameter.");

                    foreach (var id in ids)
                    {
                        await handler.HandleRemoveAsync(id, cacheable);
                    }

                    return Results.NoContent();
                })
                .Authorize(Authorize(config, EndpointNames.DeleteRange))
                .ApplyOpenApi(config, EndpointNames.DeleteRange)
                .ApplyEndpointPolicies(config, EndpointNames.DeleteRange);
        }

        if (ShouldMap(EndpointNames.Export))
        {
            group.MapGet($"{resource}/export",
                ([FromServices] ICommandQueryHandler<TResponse, TModel, TKey> handler,
                    [FromQuery] string? filter,
                    [FromQuery] string? includedProps,
                    [FromQuery] string? includeGraph,
                    [FromQuery] string? fields) =>
                    handler.HandleGetAllAsync(filter, includedProps, includeGraph, fields, cacheable: false, includeDeleted: false))
                .Authorize(Authorize(config, EndpointNames.Export))
                .ApplyOpenApi(config, EndpointNames.Export)
                .ApplyEndpointPolicies(config, EndpointNames.Export);
        }

        return group;
    }

    private static string BuildGroupPrefix(IKyrolusApiConfig<TResponse> config)
    {
        var prefix = (config.Prefix ?? string.Empty).Trim('/');
        var versionSegment = string.Empty;
        if (config.AppendVersionToPrefix && !string.IsNullOrWhiteSpace(config.ApiVersion))
        {
            var versionPrefix = string.IsNullOrWhiteSpace(config.VersionPrefix) ? "v" : config.VersionPrefix;
            versionSegment = $"{versionPrefix}{config.ApiVersion}".Trim('/');
        }

        if (string.IsNullOrEmpty(prefix)) return versionSegment;
        if (string.IsNullOrEmpty(versionSegment)) return prefix;
        return $"{prefix}/{versionSegment}";
    }

    private static IEnumerable<EndpointNames> GetEndpointsToMap(IKyrolusApiConfig<TResponse> config)
    {
        if (config.AllEndpointsExcept is not null && config.AllEndpointsExcept.Any())
            return config.AllEndpointsExcept.Where(e => e != EndpointNames.All);
        else if (config.Endpoints != null && !config.Endpoints.Contains(EndpointNames.All))
            return config.Endpoints.Count() == 1 ? config.Endpoints : config.Endpoints.Where(e => e != EndpointNames.All);
        else
            return config.Endpoints ?? [];
    }

    private static bool RequireAuthorzation(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
    {
        var endpointConfig = config.EndpointConfig.FirstOrDefault(e => e.Name == endpoint);

        if (endpointConfig is not null)
            return endpointConfig.Authorize;

        else return config.AuthorizeAllEndpoints;
    }
    private static (bool requireAuthorization, string? policy) Authorize(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
        => (RequireAuthorzation(config, endpoint), GetPolicy(config, endpoint));

    private static string? GetPolicy(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
    {
        var endpointConfig = config.EndpointConfig.FirstOrDefault(e => e.Name == endpoint);

        if (endpointConfig is not null)
            return endpointConfig.AuthorizationPolicy;

        else return config.GeneralAuthorizationPolicy;
    }
}

public static class MinimalApiAuthroizeExtensions
{
    public static RouteHandlerBuilder Authorize(this RouteHandlerBuilder builder, (bool requireAuthorization, string? policy) authorize)
    {
        if (authorize.requireAuthorization)
        {
            if (authorize.policy is not null)
                builder.RequireAuthorization(authorize.policy);
            else
                builder.RequireAuthorization();
        }
        return builder;
    }
}


