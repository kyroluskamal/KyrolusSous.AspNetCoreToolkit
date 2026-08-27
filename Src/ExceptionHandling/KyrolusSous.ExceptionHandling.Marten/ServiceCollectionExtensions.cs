using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.ExceptionHandling.Marten;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Marten exception mapper into the Kyrolus exception handling pipeline.
    /// </summary>
    public static IServiceCollection AddKyrolusMartenExceptionMapping(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusExceptionMapper, KyrolusMartenExceptionMapper>());
        return services;
    }
}
