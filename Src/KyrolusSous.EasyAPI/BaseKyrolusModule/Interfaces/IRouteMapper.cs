namespace KyrolusSous.EasyAPI.BaseKyrolusModule.Interfaces;

public interface IRouteMapper<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
    RouteGroupBuilder MapEndpoints(IEndpointRouteBuilder app, IKyrolusApiConfig<TResponse> config, ICommandQueryHandler<TResponse, TModel, TKey> commandQueryHandler);
}
