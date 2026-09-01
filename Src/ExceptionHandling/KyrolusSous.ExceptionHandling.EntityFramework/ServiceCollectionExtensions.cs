global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using KyrolusSous.ExceptionHandling.Abstractions.Models;

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
    /// <param name="configure">
    /// Optional configuration action for the shared <see cref="KyrolusExceptionHandlingOptions"/>, e.g. to opt into
    /// raw database error details in a trusted/internal environment: <c>o.IncludeRawDatabaseErrorDetails = builder.Environment.IsDevelopment()</c>.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKyrolusEntityFrameworkExceptionHandling(
        this IServiceCollection services,
        Action<KyrolusExceptionHandlingOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IKyrolusExceptionMapper, KyrolusEfExceptionMapper>());
        return services;
    }
}
