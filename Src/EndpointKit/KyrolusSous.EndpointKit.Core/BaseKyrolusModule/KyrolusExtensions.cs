using KyrolusSous.EndpointKit.Core.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
public static class KyrolusExtensions
{

    public static IServiceCollection AddKyrolus(this IServiceCollection services, Action<KyrolusModuleBuilder> configure)
    {
        services.AddKyrolusCorrelation();

        var builder = new KyrolusModuleBuilder();
        configure(builder);

        foreach (var registration in builder.Modules)
        {
            services.AddSingleton(typeof(IKyrolusModuleRegistration), registration);
            services.AddSingleton(typeof(IModuleRegistration), registration);
            TryRegisterModuleConfig(services, registration);
        }

        return services;
    }

    public static void MapKyrolus(this IEndpointRouteBuilder app)
    {
        var modules = app.ServiceProvider.GetServices<IKyrolusModuleRegistration>();

        foreach (var registration in modules)
        {
            registration.AddRoutes(app, app.ServiceProvider);
        }
    }

    private static void TryRegisterModuleConfig(IServiceCollection services, IKyrolusModuleRegistration registration)
    {
        var registrationType = registration.GetType();
        if (!registrationType.IsGenericType) return;
        if (registrationType.GetGenericTypeDefinition() != typeof(KyrolusModuleRegistration<,,>)) return;

        var responseType = registrationType.GetGenericArguments()[0];
        var configProperty = registrationType.GetProperty("Config");
        if (configProperty is null) return;

        var configInstance = configProperty.GetValue(registration);
        if (configInstance is null) return;

        var serviceType = typeof(IKyrolusApiConfig<>).MakeGenericType(responseType);
        services.AddSingleton(serviceType, configInstance);
    }
}
