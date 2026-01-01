using KyrolusSous.Mapping.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.Mapping.Mapster;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusMapster(this IServiceCollection services)
    {
        services.TryAddSingleton<IObjectMapper, MapsterObjectMapper>();
        return services;
    }
}
