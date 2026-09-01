using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
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
    /// <param name="configure">
    /// Optional configuration action for the shared <see cref="KyrolusExceptionHandlingOptions"/>, e.g. to opt into
    /// raw database error details in a trusted/internal environment: <c>o.IncludeRawDatabaseErrorDetails = builder.Environment.IsDevelopment()</c>.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKyrolusMartenExceptionMapping(
        this IServiceCollection services,
        Action<KyrolusExceptionHandlingOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusExceptionMapper, KyrolusMartenExceptionMapper>());
        return services;
    }
}
