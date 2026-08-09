namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

public class KyrolusModuleBuilder
{
    internal List<IModuleRegistration> Modules { get; } = [];

    public void AddModule<TModule, TResponse, TModel, TKey>(IKyrolusApiConfig<TResponse> config)
        where TModule : IKyrolusModule<TResponse, TModel, TKey>
        where TResponse : class
        where TModel : class
        where TKey : notnull, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(config);

        var registration = new KyrolusModuleRegistration<TResponse, TModel, TKey>(
            serviceProvider =>
            {
                var routeMapper = serviceProvider.GetRequiredService<IRouteMapper<TResponse, TModel, TKey>>();
                return ActivatorUtilities.CreateInstance<TModule>(serviceProvider, routeMapper, config);
            },
            config);

        Modules.Add(registration);
    }
}
