namespace KyrolusSous.EasyAPI.BaseKyrolusModule;

public class KyrolusModuleBuilder(IServiceProvider serviceProvider)
{

    internal List<IModuleRegistration> Modules { get; } = [];

    public void AddModule<TModule, TResponse, TModel, TKey>(
    IKyrolusApiConfig<TResponse> config)
        where TModule : IKyrolusModule<TResponse, TModel, TKey>
        where TResponse : class
        where TModel : class
        where TKey : notnull, IEquatable<TKey>
    {
        var routeMapper = serviceProvider.GetRequiredService<IRouteMapper<TResponse, TModel, TKey>>();
        var commandQueryHandler = serviceProvider.GetRequiredService<ICommandQueryHandler<TResponse, TModel, TKey>>();
        // Dynamically create the module instance and inject required services
        var module = ActivatorUtilities.CreateInstance<TModule>(serviceProvider, routeMapper, commandQueryHandler, config);

        var registration = new KyrolusModuleRegistration<TResponse, TModel, TKey>
        {
            Module = module,
            Config = config
        };

        Modules.Add(registration);
    }
}
