global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.ExceptionHandling.EntityFramework;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusEntityFrameworkExceptionHandling(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusExceptionMapper, KyrolusEfExceptionMapper>());
        return services;
    }
}
