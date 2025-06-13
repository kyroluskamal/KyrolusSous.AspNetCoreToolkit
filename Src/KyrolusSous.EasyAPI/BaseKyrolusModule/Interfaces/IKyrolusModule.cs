
global using Microsoft.AspNetCore.Routing;

namespace KyrolusSous.EasyAPI.BaseKyrolusModule.Interfaces;

public interface IKyrolusModuleBase
{

}
public interface IKyrolusModule<TResponse, TModel, TKey> : IKyrolusModuleBase
where TResponse : class
where TKey : notnull, IEquatable<TKey>
{
    IEndpointRouteBuilder AddRoutes(IEndpointRouteBuilder app);
}
