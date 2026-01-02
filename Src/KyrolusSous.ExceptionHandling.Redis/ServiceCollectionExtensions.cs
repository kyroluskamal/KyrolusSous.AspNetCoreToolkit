global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.ExceptionHandling.Redis;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusRedisExceptionHandling(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusExceptionMapper, KyrolusRedisExceptionMapper>());
        return services;
    }
}
