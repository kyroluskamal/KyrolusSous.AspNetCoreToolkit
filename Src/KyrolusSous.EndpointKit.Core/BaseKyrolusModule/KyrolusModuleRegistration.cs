namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

public class KyrolusModuleRegistration<TResponse, TModel, TKey> : IModuleRegistration
    where TResponse : class
    where TKey : notnull, IEquatable<TKey>
{
    private readonly Func<IServiceProvider, IKyrolusModule<TResponse, TModel, TKey>> moduleFactory;
    public IKyrolusApiConfig<TResponse> Config { get; }

    public KyrolusModuleRegistration(
        Func<IServiceProvider, IKyrolusModule<TResponse, TModel, TKey>> moduleFactory,
        IKyrolusApiConfig<TResponse> config)
    {
        this.moduleFactory = moduleFactory ?? throw new ArgumentNullException(nameof(moduleFactory));
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public void AddRoutes(IEndpointRouteBuilder app, IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var module = moduleFactory(scope.ServiceProvider);
        module.AddRoutes(app);
    }
}
