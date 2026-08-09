global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.ExceptionHandling.FluentValidation;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusFluentValidationExceptionHandling(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusExceptionMapper, KyrolusFluentValidationExceptionMapper>());
        return services;
    }
}
