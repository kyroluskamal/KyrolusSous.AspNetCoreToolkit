using KyrolusSous.EasyAPI.BaseKyrolusModule.Interfaces;

namespace KyrolusSous.EasyAPI.BaseKyrolusModule;

public class KyrolusModuleRegistration<TResponse, TModel, TKey> : IModuleRegistration
    where TResponse : class
    where TKey : notnull, IEquatable<TKey>
{
    public IKyrolusModule<TResponse, TModel, TKey> Module { get; set; } = default!;
    public IKyrolusApiConfig<TResponse> Config { get; set; } = default!;

    public void AddRoutes(IEndpointRouteBuilder app, IServiceProvider serviceProvider)
    {
        Module.AddRoutes(app);
    }
}