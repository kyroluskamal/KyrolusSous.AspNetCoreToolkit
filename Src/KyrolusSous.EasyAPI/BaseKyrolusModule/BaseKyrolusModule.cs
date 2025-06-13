using KyrolusSous.EasyAPI.BaseKyrolusModule.Interfaces;
using System.Net;

namespace KyrolusSous.EasyAPI.BaseKyrolusModule;

public class BaseKyrolusModule<TResponse, TModel, TKey>(IRouteMapper<TResponse, TModel, TKey> routeMapper,
ICommandQueryHandler<TResponse, TModel, TKey> commandQueryHandler,
 IKyrolusApiConfig<TResponse> config = default!) :
 IKyrolusModule<TResponse, TModel, TKey>
where TResponse : class
where TModel : class
where TKey : notnull, IEquatable<TKey>
{
    protected readonly IKyrolusApiConfig<TResponse> _config = config;

    public virtual IEndpointRouteBuilder AddRoutes(IEndpointRouteBuilder app)
    {
        return routeMapper.MapEndpoints(app, _config, commandQueryHandler);
    }

}

