namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;

/// <summary>
/// Maps endpoint routes for a Kyrolus module.
/// </summary>
public interface IKyrolusRouteMapper<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
    RouteGroupBuilder MapEndpoints(IEndpointRouteBuilder app, IKyrolusApiConfig<TResponse> config);
}

/// <summary>
/// Backward-compatibility alias for <see cref="IKyrolusRouteMapper{TResponse, TModel, TKey}"/>.
/// </summary>
public interface IRouteMapper<TResponse, TModel, TKey> : IKyrolusRouteMapper<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
}
