namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

public class DefaultRouteMapper<TResponse, TModel, TKey> : IRouteMapper<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
    public RouteGroupBuilder MapEndpoints(IEndpointRouteBuilder app, IKyrolusApiConfig<TResponse> config, ICommandQueryHandler<TResponse, TModel, TKey> commandQueryHandler)
    {
        config.Route ??= typeof(TResponse).Name;
        config.ApiName ??= typeof(TResponse).Name;
        var groupPrefix = BuildGroupPrefix(config);
        var group = app.MapGroup(groupPrefix).WithTags(config.ApiName);
        var endpointsToMap = GetEndpointsToMap(config);
        bool ShouldMap(EndpointNames currentEndpoint) => config.AllEndpointsExcept is not null ?
            !endpointsToMap.Contains(currentEndpoint) : endpointsToMap.Contains(currentEndpoint)
            || endpointsToMap.Contains(EndpointNames.All);

        if (ShouldMap(EndpointNames.GetAll))
        {
            group.MapGet($"{config.Route}s", commandQueryHandler.HandleGetAllAsync)
                .Authorize(Authorize(config, EndpointNames.GetAll))
                .ApplyOpenApi(config, EndpointNames.GetAll)
                .ApplyEndpointPolicies(config, EndpointNames.GetAll);
        }

        if (ShouldMap(EndpointNames.GetById))
        {
            group.MapGet($"/{config.Route}/{{id}}", commandQueryHandler.HandleGetByIdAsync)
                .Authorize(Authorize(config, EndpointNames.GetById))
                .ApplyOpenApi(config, EndpointNames.GetById)
                .ApplyEndpointPolicies(config, EndpointNames.GetById);
        }

        if (ShouldMap(EndpointNames.Add))
        {
            group.MapPost(config.Route, commandQueryHandler.HandleCreateAsync)
                .Authorize(Authorize(config, EndpointNames.Add))
                .ApplyOpenApi(config, EndpointNames.Add)
                .ApplyEndpointPolicies(config, EndpointNames.Add);
        }

        if (ShouldMap(EndpointNames.AddRange))
        {
            group.MapPost($"{config.Route}s", commandQueryHandler.HandleCreateRangeAsync)
                .Authorize(Authorize(config, EndpointNames.AddRange))
                .ApplyOpenApi(config, EndpointNames.AddRange)
                .ApplyEndpointPolicies(config, EndpointNames.AddRange);
        }

        if (ShouldMap(EndpointNames.Update))
        {
            group.MapPut($"/{config.Route}/{{id}}", commandQueryHandler.HandleUpdateAsync)
                .Authorize(Authorize(config, EndpointNames.Update))
                .ApplyOpenApi(config, EndpointNames.Update)
                .ApplyEndpointPolicies(config, EndpointNames.Update);
        }

        if (ShouldMap(EndpointNames.Patch))
        {
            group.MapPatch($"/{config.Route}/{{id}}", commandQueryHandler.HandlePatchAsync)
                .Authorize(Authorize(config, EndpointNames.Patch))
                .ApplyOpenApi(config, EndpointNames.Patch)
                .ApplyEndpointPolicies(config, EndpointNames.Patch);
        }

        if (ShouldMap(EndpointNames.UpdateRange))
        {
            group.MapPut($"/{config.Route}s", commandQueryHandler.HandleUpdateRangeAsync)
                .Authorize(Authorize(config, EndpointNames.UpdateRange))
                .ApplyOpenApi(config, EndpointNames.UpdateRange)
                .ApplyEndpointPolicies(config, EndpointNames.UpdateRange);
        }

        if (ShouldMap(EndpointNames.Delete))
        {
            group.MapDelete($"/{config.Route}/{{id}}", commandQueryHandler.HandleRemoveAsync)
                .Authorize(Authorize(config, EndpointNames.Delete))
                .ApplyOpenApi(config, EndpointNames.Delete)
                .ApplyEndpointPolicies(config, EndpointNames.Delete);
        }

        if (ShouldMap(EndpointNames.DeleteRange))
        {
            group.MapDelete($"{config.Route}s", commandQueryHandler.HandleRemoveRangeAsync)
                .Authorize(Authorize(config, EndpointNames.DeleteRange))
                .ApplyOpenApi(config, EndpointNames.DeleteRange)
                .ApplyEndpointPolicies(config, EndpointNames.DeleteRange);
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


