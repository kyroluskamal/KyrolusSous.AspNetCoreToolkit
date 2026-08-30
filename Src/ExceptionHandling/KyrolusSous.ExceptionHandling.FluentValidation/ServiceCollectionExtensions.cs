global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.ExceptionHandling.FluentValidation;

/// <summary>
/// Provides extension methods for registering FluentValidation exception mappers.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="KyrolusFluentValidationExceptionMapper"/> into the DI container to translate FluentValidation exceptions.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for FluentValidation options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKyrolusFluentValidationExceptionHandling(
        this IServiceCollection services,
        Action<KyrolusFluentValidationOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusExceptionMapper, KyrolusFluentValidationExceptionMapper>());
        return services;
    }
}
