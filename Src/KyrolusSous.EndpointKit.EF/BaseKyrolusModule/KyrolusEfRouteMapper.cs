using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
using KyrolusSous.EndpointKit.EF.BaseKyrolusModule.Interfaces;

namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule;

public sealed class KyrolusEfRouteMapper<TResponse, TModel, TKey> : IRouteMapper<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
    private readonly DefaultRouteMapper<TResponse, TModel, TKey> coreMapper = new();

    public RouteGroupBuilder MapEndpoints(
        IEndpointRouteBuilder app,
        IKyrolusApiConfig<TResponse> config,
        ICommandQueryHandler<TResponse, TModel, TKey> commandQueryHandler)
    {
        var group = coreMapper.MapEndpoints(app, config, commandQueryHandler);

        if (commandQueryHandler is not IKyrolusEfCommandQueryHandler<TResponse, TModel, TKey> efHandler)
        {
            return group;
        }

        var efConfig = config as IKyrolusEfApiConfig<TResponse>;
        if (efConfig is { EnableQueryEndpoints: true })
        {
            group.MapPost($"/{config.Route}s/query", efHandler.HandleQueryAsync)
                .Authorize(Authorize(config, EndpointNames.Query));
        }

        if (efConfig is { EnablePagedEndpoints: true })
        {
            group.MapGet($"{config.Route}s/paged", efHandler.HandleGetAllPagedAsync)
                .Authorize(Authorize(config, EndpointNames.Paged));
            group.MapPost($"/{config.Route}s/query/paged", efHandler.HandleQueryPagedAsync)
                .Authorize(Authorize(config, EndpointNames.QueryPaged));
        }

        return group;
    }

    private static (bool requireAuthorization, string? policy) Authorize(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
        => (RequireAuthorzation(config, endpoint), GetPolicy(config, endpoint));

    private static bool RequireAuthorzation(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
    {
        var endpointConfig = config.EndpointConfig.FirstOrDefault(e => e.Name == endpoint);

        if (endpointConfig is not null)
            return endpointConfig.Authorize;

        return config.AuthorizeAllEndpoints;
    }

    private static string? GetPolicy(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
    {
        var endpointConfig = config.EndpointConfig.FirstOrDefault(e => e.Name == endpoint);

        if (endpointConfig is not null)
            return endpointConfig.AuthorizationPolicy;

        return config.GeneralAuthorizationPolicy;
    }
}
