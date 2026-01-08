using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
using KyrolusSous.EndpointKit.EF.BaseKyrolusModule;

namespace KyrolusSous.EndpointKit.EF.Config;

public static class AddKyrolusDefaultsExtension
{
    public static IServiceCollection AddKyrolusDefaults(this IServiceCollection services)
    {
        services.AddSingleton<KyrolusModuleBuilder>();
        services.AddScoped(typeof(IKyrolusApiConfig<>), typeof(KyrolusEfApiConfig<>));
        services.AddScoped(typeof(IRouteMapper<,,>), typeof(KyrolusEfRouteMapper<,,>));
        services.AddScoped<IKyrolusMapper, KyrolusMapper>();
        services.AddScoped(typeof(ICommandQueryHandler<,,>), typeof(DefaultCommandQueryHandler<,,>));
        return services;
    }
}
