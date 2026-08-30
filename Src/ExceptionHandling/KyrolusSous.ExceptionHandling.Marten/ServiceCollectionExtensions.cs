using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.ExceptionHandling.Marten;

/// <summary>
/// Provides extension methods for registering Marten document database exception mappers.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="KyrolusMartenExceptionMapper"/> into the DI container to translate Marten exceptions.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKyrolusMartenExceptionMapping(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusExceptionMapper, KyrolusMartenExceptionMapper>());
        return services;
    }
}
