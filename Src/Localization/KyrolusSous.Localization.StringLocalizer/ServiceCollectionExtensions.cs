using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Localization.StringLocalizer;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IKyrolusLocalizer"/> backed by an ASP.NET Core <see cref="IStringLocalizer{TResource}"/>
    /// already registered in the container.
    /// </summary>
    public static IServiceCollection AddKyrolusStringLocalizerLocalization<TResource>(this IServiceCollection services)
    {
        services.TryAddSingleton<IKyrolusLocalizer>(sp =>
            new KyrolusStringLocalizerAdapter(sp.GetRequiredService<IStringLocalizer<TResource>>()));

        return services;
    }
}
