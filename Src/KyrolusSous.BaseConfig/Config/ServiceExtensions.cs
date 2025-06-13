global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.AspNetCore.Http;
namespace KyrolusSous.BaseConfig.Config;

public static class ServiceExtensions
{
    public static void AddBaseServices(this IServiceCollection services)
    {
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddEndpointsApiExplorer();
        services.AddSingleton(TimeProvider.System);
    }
}
