using System.ComponentModel;
using KyrolusSous.EasyAPI.BaseKyrolusModule.Enum;
using KyrolusSous.EasyAPI.BaseKyrolusModule.Interfaces;
using Serilog;

namespace KyrolusSous.EasyAPI.BaseKyrolusModule;

public class DefaultRouteMapper<TResponse, TModel, TKey> : IRouteMapper<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
    public RouteGroupBuilder MapEndpoints(IEndpointRouteBuilder app, IKyrolusApiConfig<TResponse> config, ICommandQueryHandler<TResponse, TModel, TKey> commandQueryHandler)
    {
        var group = app.MapGroup(config.Prefix ?? "").WithTags(config.ApiName ?? typeof(TResponse).Name);
        config.Route ??= typeof(TResponse).Name;
        config.ApiName ??= typeof(TResponse).Name;
        var endpointsToMap = GetEndpointsToMap(config);
        bool ShouldMap(EndpointNames currentEndpoint) => config.AllEndpointsExcept is not null ?
            !endpointsToMap.Contains(currentEndpoint) : endpointsToMap.Contains(currentEndpoint)
            || endpointsToMap.Contains(EndpointNames.All);

        _ = ShouldMap(EndpointNames.GetAll) ? group.MapGet($"{config.Route}s", commandQueryHandler.HandleGetAllAsync).Authorize(Authorize(config, EndpointNames.GetById)) : null;
        _ = ShouldMap(EndpointNames.GetById) ? group.MapGet($"/{config.Route}/{{id}}", commandQueryHandler.HandleGetByIdAsync).Authorize(Authorize(config, EndpointNames.GetById)) : null;
        _ = ShouldMap(EndpointNames.Add) ? group.MapPost(config.Route, commandQueryHandler.HandleCreateAsync).Authorize(Authorize(config, EndpointNames.Add)) : null;
        _ = ShouldMap(EndpointNames.AddRange) ? group.MapPost($"{config.Route}s", commandQueryHandler.HandleCreateRangeAsync).Authorize(Authorize(config, EndpointNames.AddRange)) : null;
        _ = ShouldMap(EndpointNames.Update) ? group.MapPut($"/{config.Route}/{{id}}", commandQueryHandler.HandleUpdateAsync).Authorize(Authorize(config, EndpointNames.Update)) : null;
        _ = ShouldMap(EndpointNames.Patch) ? group.MapPatch($"/{config.Route}/{{id}}", commandQueryHandler.HandlePatchAsync).Authorize(Authorize(config, EndpointNames.Patch)) : null;
        _ = ShouldMap(EndpointNames.UpdateRange) ? group.MapPut($"/{config.Route}s", commandQueryHandler.HandleUpdateRangeAsync).Authorize(Authorize(config, EndpointNames.UpdateRange)) : null;
        _ = ShouldMap(EndpointNames.Delete) ? group.MapDelete($"/{config.Route}/{{id}}", commandQueryHandler.HandleRemoveAsync).Authorize(Authorize(config, EndpointNames.Delete)) : null;
        _ = ShouldMap(EndpointNames.DeleteRange) ? group.MapDelete($"{config.Route}s", commandQueryHandler.HandleRemoveRangeAsync).Authorize(Authorize(config, EndpointNames.DeleteRange)) : null;

        return group;
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

static class MinimalApiAuthroizeExtensions
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
