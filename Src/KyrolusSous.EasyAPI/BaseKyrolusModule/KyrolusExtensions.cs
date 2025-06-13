using KyrolusSous.EasyAPI.BaseKyrolusModule.Interfaces;

namespace KyrolusSous.EasyAPI.BaseKyrolusModule;
public static class KyrolusExtensions
{

    // Scan assemblies for IKyrolusModule implementations
    private static readonly List<IModuleRegistration> _modules = new List<IModuleRegistration>();

    public static IServiceCollection AddKyrolus(this IServiceCollection services, Action<KyrolusModuleBuilder> configure)
    {
        var builder = new KyrolusModuleBuilder(services.BuildServiceProvider());
        configure(builder);

        _modules.AddRange(builder.Modules);

        return services;
    }

    internal static IEnumerable<IModuleRegistration> GetRegisteredModules()
    {
        return _modules;
    }

    public static void MapKyrolus(this IEndpointRouteBuilder app)
    {
        var modules = GetRegisteredModules();

        foreach (var registration in modules)
        {
            registration.AddRoutes(app, app.ServiceProvider);
        }
    }
}
