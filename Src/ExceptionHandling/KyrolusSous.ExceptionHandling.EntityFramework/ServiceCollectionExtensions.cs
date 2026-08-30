global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.ExceptionHandling.EntityFramework;

/// <summary>
/// Provides extension methods for registering Entity Framework Core exception mappers.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="KyrolusEfExceptionMapper"/> into the DI container to translate EF Core exceptions.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKyrolusEntityFrameworkExceptionHandling(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusExceptionMapper, KyrolusEfExceptionMapper>());
        return services;
    }
}
