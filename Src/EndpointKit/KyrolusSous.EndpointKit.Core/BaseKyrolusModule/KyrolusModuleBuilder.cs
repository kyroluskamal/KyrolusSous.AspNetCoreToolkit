namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

public class KyrolusModuleBuilder
{
    internal List<IKyrolusModuleRegistration> Modules { get; } = [];

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
                var routeMapper = serviceProvider.GetRequiredService<IKyrolusRouteMapper<TResponse, TModel, TKey>>();
                return ActivatorUtilities.CreateInstance<TModule>(serviceProvider, routeMapper, config);
            },
            config);

        Modules.Add(registration);
    }

    public void AddCustomModule(IKyrolusModuleRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        Modules.Add(registration);
    }

    public void AddEndpointModule(Action<IEndpointRouteBuilder, IServiceProvider> mapAction)
    {
        ArgumentNullException.ThrowIfNull(mapAction);
        Modules.Add(new KyrolusActionModuleRegistration(mapAction));
    }

    public void AddEndpointModule(Action<IEndpointRouteBuilder> mapAction)
    {
        ArgumentNullException.ThrowIfNull(mapAction);
        Modules.Add(new KyrolusActionModuleRegistration((app, _) => mapAction(app)));
    }
}

internal sealed class KyrolusActionModuleRegistration(Action<IEndpointRouteBuilder, IServiceProvider> mapAction) : IKyrolusModuleRegistration
{
    public void AddRoutes(IEndpointRouteBuilder app, IServiceProvider serviceProvider)
    {
        mapAction(app, serviceProvider);
    }
}
