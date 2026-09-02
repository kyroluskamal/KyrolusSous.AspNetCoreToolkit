using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Localization.StringLocalizer;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IKyrolusLocalizer"/> and <see cref="IKyrolusLocalizer{TResource}"/>, both backed by
    /// an ASP.NET Core <see cref="IStringLocalizer{TResource}"/> already registered in the container. The
    /// non-generic <see cref="IKyrolusLocalizer"/> registration uses <c>TryAddSingleton</c>, so calling this
    /// method again for a different <typeparamref name="TResource"/> leaves the first-registered resource as
    /// the untyped default - resolve <see cref="IKyrolusLocalizer{TResource}"/> instead when you need a
    /// specific one.
    /// </summary>
    public static IServiceCollection AddKyrolusStringLocalizerLocalization<TResource>(this IServiceCollection services)
    {
        services.TryAddSingleton<IKyrolusLocalizer>(sp =>
            new KyrolusStringLocalizerAdapter(sp.GetRequiredService<IStringLocalizer<TResource>>()));
        services.TryAddSingleton<IKyrolusLocalizer<TResource>>(sp =>
            new KyrolusStringLocalizerAdapter<TResource>(sp.GetRequiredService<IStringLocalizer<TResource>>()));

        return services;
    }
}
