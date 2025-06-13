using KyrolusSous.EasyAPI.BaseKyrolusModule;
using KyrolusSous.EasyAPI.BaseKyrolusModule.Interfaces;

namespace KyrolusSous.EasyAPI.Config;

public static class AddKyrolusDefaultsExtension
{
    public static IServiceCollection AddKyrolusDefaults(this IServiceCollection services)
    {
        services.AddSingleton<KyrolusModuleBuilder>();
        services.AddScoped(typeof(IKyrolusApiConfig<>), typeof(ApiKyrolusApiConfig<>));
        services.AddScoped(typeof(IRouteMapper<,,>), typeof(DefaultRouteMapper<,,>));
        services.AddScoped<IKyrolusMapper, KyrolusMapper>();
        services.AddScoped(typeof(ICommandQueryHandler<,,>), typeof(DefaultCommandQueryHandler<,,>));
        return services;
    }
}
