using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;

public sealed class KyrolusMartenRouteMapper<TResponse, TModel, TKey> : IRouteMapper<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
    private readonly DefaultRouteMapper<TResponse, TModel, TKey> coreMapper = new();

    public RouteGroupBuilder MapEndpoints(
        IEndpointRouteBuilder app,
        IKyrolusApiConfig<TResponse> config)
    {
        var martenConfig = config as IKyrolusMartenApiConfig<TResponse>;
        var compositeKeyOnly = martenConfig?.CompositeKeyOnly == true;
        var originalAllEndpointsExcept = config.AllEndpointsExcept ?? Array.Empty<EndpointNames>();
        var resource = $"{config.Route}s";
        if (compositeKeyOnly)
        {
            var excluded = new HashSet<EndpointNames>(originalAllEndpointsExcept ?? []);
            excluded.Add(EndpointNames.GetById);
            excluded.Add(EndpointNames.Update);
            excluded.Add(EndpointNames.Patch);
            excluded.Add(EndpointNames.Delete);
            config.AllEndpointsExcept = excluded.ToArray();
        }

        var group = coreMapper.MapEndpoints(app, config);
        var endpointsToMap = GetEndpointsToMap(config);
        var useExclusions = config.AllEndpointsExcept is not null && config.AllEndpointsExcept.Any();
        bool ShouldMap(EndpointNames currentEndpoint) => useExclusions ?
            !endpointsToMap.Contains(currentEndpoint) : endpointsToMap.Contains(currentEndpoint)
            || endpointsToMap.Contains(EndpointNames.All);

        // Marten route mapper assumes a Marten command/query handler is registered in DI.

        if (martenConfig is { EnableHeadEndpoint: true } && ShouldMap(EndpointNames.GetById))
        {
            group.MapMethods($"{resource}/{{id}}", ["HEAD"],
                    ([FromServices] IKyrolusMartenCommandQueryHandler<TResponse, TModel, TKey> handler,
                        TKey id,
                        CancellationToken cancellationToken) =>
                        handler.HandleHeadByIdAsync(id, cancellationToken))
                .Authorize(Authorize(config, EndpointNames.Head))
                .ApplyOpenApi(config, EndpointNames.Head)
                .ApplyEndpointPolicies(config, EndpointNames.Head);
        }

        if (compositeKeyOnly)
        {
            config.AllEndpointsExcept = originalAllEndpointsExcept ?? [];
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

    private static IEnumerable<EndpointNames> GetEndpointsToMap(IKyrolusApiConfig<TResponse> config)
    {
        if (config.AllEndpointsExcept is not null && config.AllEndpointsExcept.Any())
            return config.AllEndpointsExcept.Where(e => e != EndpointNames.All);
        else if (config.Endpoints != null && !config.Endpoints.Contains(EndpointNames.All))
            return config.Endpoints.Count() == 1 ? config.Endpoints : config.Endpoints.Where(e => e != EndpointNames.All);
        else
            return config.Endpoints ?? [];
    }
}
